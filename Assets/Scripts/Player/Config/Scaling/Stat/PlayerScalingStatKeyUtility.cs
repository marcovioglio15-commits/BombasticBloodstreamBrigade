#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Text;
using UnityEditor;

/// <summary>
/// Provides stable key generation and lookup helpers for serialized properties used by scaling rules.
/// </summary>
public static class PlayerScalingStatKeyUtility
{
    #region Constants
    private static readonly string[] StableStringIdPropertyNames =
    {
        "powerUpId",
        "moduleId",
        "bindingId",
        "presetId",
        "statName",
        "scheduleId",
        "phaseID",
        "rankId",
        "passivePowerUpId",
        "weaponId",
        "animationId"
    };

    private static readonly string[] StableNestedStringIdPropertyPaths =
    {
        "commonData.powerUpId"
    };

    private static readonly string[] StableIntegerIdPropertyNames =
    {
        "milestoneLevel",
        "stepIndex"
    };
    #endregion

    #region Methods
    #region Public Methods
    /// <summary>
    /// Builds a normalized stat key for a serialized property supported by Add Scaling.
    /// </summary>
    /// <param name="property">Serialized property to convert into a stable key.</param>
    /// <returns>Normalized key string, or empty when property is invalid.</returns>
    public static string BuildStatKey(SerializedProperty property)
    {
        if (property == null)
            return string.Empty;

        if (property.serializedObject == null)
            return string.Empty;

        return NormalizePropertyPath(property.serializedObject, property.propertyPath);
    }

    /// <summary>
    /// Resolves a serialized property from a previously generated stat key.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the target property.</param>
    /// <param name="statKey">Key produced by BuildStatKey.</param>
    /// <param name="property">Resolved property when found.</param>
    /// <returns>True when a scaling-supported property was resolved, otherwise false.</returns>
    public static bool TryFindPropertyByStatKey(SerializedObject serializedObject,
                                                string statKey,
                                                out SerializedProperty property)
    {
        property = null;
        if (serializedObject == null)
            return false;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;
        string resolvedStatKey = ApplyKnownAliases(statKey);
        SerializedProperty directProperty = serializedObject.FindProperty(resolvedStatKey);

        if (directProperty != null && IsScalingSupportedProperty(directProperty))
        {
            property = directProperty;
            return true;
        }

        if (TryResolveStablePathToProperty(serializedObject, resolvedStatKey, out SerializedProperty stableProperty) &&
            stableProperty != null &&
            IsScalingSupportedProperty(stableProperty))
        {
            property = stableProperty;
            return true;
        }

        return TryFindPropertyByIteratorScan(serializedObject, resolvedStatKey, out property);
    }

    /// <summary>
    /// Checks whether a serialized property is a supported numeric type for scaling.
    /// </summary>
    /// <param name="property">Property to inspect.</param>
    /// <returns>True when the property is integer or float, otherwise false.</returns>
    public static bool IsNumericProperty(SerializedProperty property)
    {
        if (property == null)
            return false;

        if (property.propertyType == SerializedPropertyType.Integer)
            return true;

        return property.propertyType == SerializedPropertyType.Float;
    }

    /// <summary>
    /// Checks whether a serialized property is supported by Add Scaling.
    /// </summary>
    /// <param name="property">Property to inspect.</param>
    /// <returns>True when the property is numeric, boolean, token string or enum-backed.</returns>
    public static bool IsScalingSupportedProperty(SerializedProperty property)
    {
        if (property == null)
            return false;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Float:
            case SerializedPropertyType.Boolean:
            case SerializedPropertyType.String:
            case SerializedPropertyType.Enum:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Converts a raw property path into normalized representation with stable list tokens when possible.
    /// </summary>
    /// <param name="serializedObject">Serialized object used to inspect list element IDs.</param>
    /// <param name="propertyPath">Unity raw property path.</param>
    /// <returns>Normalized path suitable as persistent stat key.</returns>
    public static string NormalizePropertyPath(SerializedObject serializedObject, string propertyPath)
    {
        if (serializedObject == null)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(propertyPath))
            return string.Empty;

        string[] segments = propertyPath.Split('.');
        StringBuilder outputBuilder = new StringBuilder(propertyPath.Length + 32);
        StringBuilder currentPathBuilder = new StringBuilder(propertyPath.Length + 32);
        int segmentIndex = 0;

        while (segmentIndex < segments.Length)
        {
            string currentSegment = segments[segmentIndex];

            if (currentSegment == "Array" &&
                segmentIndex + 1 < segments.Length &&
                segments[segmentIndex + 1].StartsWith("data[", StringComparison.Ordinal))
            {
                string dataSegment = segments[segmentIndex + 1];
                AppendSegment(outputBuilder, currentSegment);
                AppendSegment(currentPathBuilder, currentSegment);
                AppendSegment(outputBuilder, dataSegment);
                AppendSegment(currentPathBuilder, dataSegment);

                string elementPath = currentPathBuilder.ToString();
                SerializedProperty arrayElementProperty = serializedObject.FindProperty(elementPath);
                string stableToken = ResolveStableArrayElementToken(arrayElementProperty);

                if (!string.IsNullOrWhiteSpace(stableToken))
                {
                    string resolvedDataSegment = BuildStableDataSegmentWithFallbackIndex(dataSegment, stableToken);
                    ReplaceLastArrayToken(outputBuilder, resolvedDataSegment);
                }

                segmentIndex += 2;
                continue;
            }

            AppendSegment(outputBuilder, NormalizeOutputSegment(currentSegment));
            AppendSegment(currentPathBuilder, currentSegment);
            segmentIndex += 1;
        }

        return outputBuilder.ToString();
    }

    /// <summary>
    /// Normalizes one stat key so private Unity-style backing segments such as m_Field become field.
    /// </summary>
    /// <param name="statKey">Stat key to normalize.</param>
    /// <returns>Normalized stat key preserving array and stable-token syntax.</returns>
    public static string NormalizeStatKey(string statKey)
    {
        if (string.IsNullOrWhiteSpace(statKey))
            return string.Empty;

        statKey = ApplyKnownAliases(statKey);
        string[] segments = statKey.Split('.');
        StringBuilder builder = new StringBuilder(statKey.Length);

        for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            if (segmentIndex > 0)
                builder.Append('.');

            builder.Append(NormalizeOutputSegment(segments[segmentIndex]));
        }

        return builder.ToString();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Applies serialized-path aliases kept for backward compatibility with renamed scalable fields.
    /// </summary>
    /// <param name="statKey">Incoming stat key.</param>
    /// <returns>Aliased stat key when a known rename exists; otherwise the original key.</returns>
    private static string ApplyKnownAliases(string statKey)
    {
        if (string.IsNullOrWhiteSpace(statKey))
            return string.Empty;

        statKey = statKey.Replace(".visualPalette", ".visualPresetId", StringComparison.Ordinal);
        statKey = statKey.Replace(".tickPulseTravelSpeed", ".stormTwistSpeed", StringComparison.Ordinal);
        statKey = statKey.Replace(".tickPulseLength", ".stormTickPostTravelHoldSeconds", StringComparison.Ordinal);
        statKey = statKey.Replace(".stormBurstDurationSeconds", ".stormTickPostTravelHoldSeconds", StringComparison.Ordinal);
        statKey = statKey.Replace(".tickPulseWidthBoost", ".stormIdleIntensity", StringComparison.Ordinal);
        statKey = statKey.Replace(".tickPulseBrightnessBoost", ".stormBurstIntensity", StringComparison.Ordinal);
        statKey = statKey.Replace(".impactShape", ".terminalCapShape", StringComparison.Ordinal);
        return statKey.Replace(".impactScaleMultiplier", ".terminalCapScaleMultiplier", StringComparison.Ordinal);
    }

    private static void AppendSegment(StringBuilder builder, string segment)
    {
        if (builder.Length > 0)
            builder.Append('.');

        builder.Append(segment);
    }

    private static string NormalizeOutputSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return string.Empty;

        if (!segment.StartsWith("m_", StringComparison.Ordinal) || segment.Length <= 2)
            return segment;

        string strippedSegment = segment.Substring(2);

        if (strippedSegment.Length == 1)
            return strippedSegment.ToLowerInvariant();

        return char.ToLowerInvariant(strippedSegment[0]) + strippedSegment.Substring(1);
    }

    /// <summary>
    /// Scans all serialized properties, including non-visible ones, to resolve stat keys emitted from custom drawers.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing candidate properties.</param>
    /// <param name="statKey">Stable key to resolve.</param>
    /// <param name="property">Resolved property when a match is found.</param>
    /// <returns>True when a matching scaling-supported property is found; otherwise false.</returns>
    private static bool TryFindPropertyByIteratorScan(SerializedObject serializedObject,
                                                      string statKey,
                                                      out SerializedProperty property)
    {
        property = null;
        if (serializedObject == null)
            return false;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;
        SerializedProperty iterator = serializedObject.GetIterator();

        if (iterator == null)
            return false;
        bool enterChildren = true;

        while (iterator.Next(enterChildren))
        {
            enterChildren = true;

            if (!IsScalingSupportedProperty(iterator))
                continue;

            string iteratorKey = BuildStatKey(iterator);

            if (!string.Equals(iteratorKey, statKey, StringComparison.OrdinalIgnoreCase))
                continue;

            property = iterator.Copy();
            return true;
        }

        return false;
    }

    private static bool TryResolveStablePathToProperty(SerializedObject serializedObject,
                                                       string statKey,
                                                       out SerializedProperty property)
    {
        property = null;
        if (serializedObject == null)
            return false;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;
        string[] pathSegments = statKey.Split('.');

        if (pathSegments == null || pathSegments.Length == 0)
            return false;

        StringBuilder resolvedPathBuilder = new StringBuilder(statKey.Length);
        int segmentIndex = 0;

        while (segmentIndex < pathSegments.Length)
        {
            string currentSegment = pathSegments[segmentIndex];

            if (string.Equals(currentSegment, "Array", StringComparison.Ordinal) &&
                segmentIndex + 1 < pathSegments.Length &&
                TryResolveArrayDataSegment(pathSegments[segmentIndex + 1],
                                           serializedObject,
                                           resolvedPathBuilder,
                                           out string resolvedArrayDataSegment))
            {
                AppendSegment(resolvedPathBuilder, "Array");
                AppendSegment(resolvedPathBuilder, resolvedArrayDataSegment);
                segmentIndex += 2;
                continue;
            }

            AppendSegment(resolvedPathBuilder, currentSegment);
            segmentIndex += 1;
        }

        string resolvedPath = resolvedPathBuilder.ToString();

        if (string.IsNullOrWhiteSpace(resolvedPath))
            return false;

        property = serializedObject.FindProperty(resolvedPath);
        return property != null;
    }

    private static bool TryResolveArrayDataSegment(string dataSegment,
                                                   SerializedObject serializedObject,
                                                   StringBuilder resolvedPathBuilder,
                                                   out string resolvedArrayDataSegment)
    {
        resolvedArrayDataSegment = string.Empty;

        if (string.IsNullOrWhiteSpace(dataSegment))
            return false;

        if (TryParseNumericArrayDataToken(dataSegment, out int numericIndex))
        {
            resolvedArrayDataSegment = string.Format("data[{0}]", numericIndex);
            return true;
        }

        if (!TryParseStableArrayDataToken(dataSegment,
                                          out int fallbackIndex,
                                          out string idPropertyName,
                                          out string idPropertyValue))
            return false;

        string arrayPath = resolvedPathBuilder.ToString();

        if (string.IsNullOrWhiteSpace(arrayPath))
            return false;

        SerializedProperty arrayProperty = serializedObject.FindProperty(arrayPath);

        if (arrayProperty == null || !arrayProperty.isArray)
            return false;

        int resolvedIndex = FindArrayElementIndexByStableId(arrayProperty,
                                                            idPropertyName,
                                                            idPropertyValue,
                                                            fallbackIndex);

        if (resolvedIndex >= 0)
        {
            resolvedArrayDataSegment = string.Format("data[{0}]", resolvedIndex);
            return true;
        }

        if (fallbackIndex < 0 || fallbackIndex >= arrayProperty.arraySize)
            return false;

        resolvedArrayDataSegment = string.Format("data[{0}]", fallbackIndex);
        return true;
    }

    private static bool TryParseNumericArrayDataToken(string dataSegment, out int index)
    {
        index = -1;

        if (!dataSegment.StartsWith("data[", StringComparison.Ordinal))
            return false;

        if (!dataSegment.EndsWith("]", StringComparison.Ordinal))
            return false;

        string indexText = dataSegment.Substring(5, dataSegment.Length - 6);

        if (!int.TryParse(indexText, out int parsedIndex))
            return false;

        index = parsedIndex;
        return true;
    }

    private static bool TryParseStableArrayDataToken(string dataSegment,
                                                     out int fallbackIndex,
                                                     out string idPropertyName,
                                                     out string idPropertyValue)
    {
        fallbackIndex = -1;
        idPropertyName = string.Empty;
        idPropertyValue = string.Empty;

        if (!dataSegment.StartsWith("data[", StringComparison.Ordinal))
            return false;

        if (!dataSegment.EndsWith("]", StringComparison.Ordinal))
            return false;

        string tokenContent = dataSegment.Substring(5, dataSegment.Length - 6);
        string stableToken = tokenContent;
        int fallbackSeparatorIndex = tokenContent.IndexOf('|');

        if (fallbackSeparatorIndex > 0 && fallbackSeparatorIndex < tokenContent.Length - 1)
        {
            string fallbackIndexText = tokenContent.Substring(0, fallbackSeparatorIndex).Trim();
            string stableTokenCandidate = tokenContent.Substring(fallbackSeparatorIndex + 1).Trim();

            if (int.TryParse(fallbackIndexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedFallbackIndex))
                fallbackIndex = parsedFallbackIndex;

            stableToken = stableTokenCandidate;
        }

        int separatorIndex = stableToken.IndexOf(':');

        if (separatorIndex <= 0 || separatorIndex >= stableToken.Length - 1)
            return false;

        idPropertyName = stableToken.Substring(0, separatorIndex).Trim();
        idPropertyValue = stableToken.Substring(separatorIndex + 1).Trim();

        if (string.IsNullOrWhiteSpace(idPropertyName))
            return false;

        if (string.IsNullOrWhiteSpace(idPropertyValue))
            return false;

        return true;
    }

    private static int FindArrayElementIndexByStableId(SerializedProperty arrayProperty,
                                                       string idPropertyName,
                                                       string idPropertyValue,
                                                       int preferredIndex)
    {
        if (arrayProperty == null || !arrayProperty.isArray)
            return -1;

        if (string.IsNullOrWhiteSpace(idPropertyName) || string.IsNullOrWhiteSpace(idPropertyValue))
            return -1;

        if (preferredIndex >= 0 &&
            preferredIndex < arrayProperty.arraySize &&
            IsArrayElementMatchingStableId(arrayProperty.GetArrayElementAtIndex(preferredIndex),
                                          idPropertyName,
                                          idPropertyValue))
        {
            return preferredIndex;
        }

        for (int elementIndex = 0; elementIndex < arrayProperty.arraySize; elementIndex++)
        {
            if (!IsArrayElementMatchingStableId(arrayProperty.GetArrayElementAtIndex(elementIndex),
                                               idPropertyName,
                                               idPropertyValue))
            {
                continue;
            }

            return elementIndex;
        }

        return -1;
    }

    private static bool IsArrayElementMatchingStableId(SerializedProperty arrayElement,
                                                       string idPropertyName,
                                                       string idPropertyValue)
    {
        if (arrayElement == null)
            return false;

        if (string.IsNullOrWhiteSpace(idPropertyName))
            return false;

        SerializedProperty idProperty = ResolveStableIdProperty(arrayElement, idPropertyName);

        if (idProperty == null)
            return false;

        return IsMatchingStableIdProperty(idProperty, idPropertyValue);
    }

    /// <summary>
    /// Resolves the serialized property used as stable identifier for one array element.
    /// Supports both direct fields and flattened token names emitted from nested stable ID paths.
    /// </summary>
    /// <param name="arrayElement">Array element that owns the identifier field.</param>
    /// <param name="idPropertyName">Stable token name stored inside the stat key.</param>
    /// <returns>Matching serialized identifier property when found; otherwise null.</returns>
    private static SerializedProperty ResolveStableIdProperty(SerializedProperty arrayElement, string idPropertyName)
    {
        if (arrayElement == null)
            return null;

        if (string.IsNullOrWhiteSpace(idPropertyName))
            return null;

        // Check the direct relative field first because most stable IDs are stored at the array-element root.
        SerializedProperty directProperty = arrayElement.FindPropertyRelative(idPropertyName);

        if (directProperty != null)
            return directProperty;

        // Nested stable IDs are flattened to their terminal token when the stat key is generated.
        for (int candidateIndex = 0; candidateIndex < StableNestedStringIdPropertyPaths.Length; candidateIndex++)
        {
            string candidatePath = StableNestedStringIdPropertyPaths[candidateIndex];
            string candidateTokenName = ResolveStableTokenName(candidatePath);

            if (!string.Equals(candidateTokenName, idPropertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            SerializedProperty nestedProperty = arrayElement.FindPropertyRelative(candidatePath);

            if (nestedProperty != null)
                return nestedProperty;
        }

        return null;
    }

    private static bool IsMatchingStableIdProperty(SerializedProperty idProperty, string tokenValue)
    {
        if (idProperty == null)
            return false;

        if (string.IsNullOrWhiteSpace(tokenValue))
            return false;

        if (idProperty.propertyType == SerializedPropertyType.String)
        {
            string candidateValue = idProperty.stringValue;

            if (string.IsNullOrWhiteSpace(candidateValue))
                return false;

            return string.Equals(candidateValue.Trim(), tokenValue, StringComparison.OrdinalIgnoreCase);
        }

        if (idProperty.propertyType == SerializedPropertyType.Integer)
        {
            if (!int.TryParse(tokenValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInteger))
                return false;

            return idProperty.intValue == parsedInteger;
        }

        return false;
    }

    private static string ResolveStableArrayElementToken(SerializedProperty arrayElementProperty)
    {
        if (arrayElementProperty == null)
            return string.Empty;

        for (int candidateIndex = 0; candidateIndex < StableStringIdPropertyNames.Length; candidateIndex++)
        {
            string candidateName = StableStringIdPropertyNames[candidateIndex];
            SerializedProperty candidateProperty = arrayElementProperty.FindPropertyRelative(candidateName);

            if (candidateProperty == null)
                continue;

            if (candidateProperty.propertyType != SerializedPropertyType.String)
                continue;

            if (string.IsNullOrWhiteSpace(candidateProperty.stringValue))
                continue;

            string sanitizedValue = candidateProperty.stringValue.Trim();
            return string.Format("{0}:{1}", candidateName, sanitizedValue);
        }

        for (int candidateIndex = 0; candidateIndex < StableNestedStringIdPropertyPaths.Length; candidateIndex++)
        {
            string candidatePath = StableNestedStringIdPropertyPaths[candidateIndex];
            SerializedProperty candidateProperty = arrayElementProperty.FindPropertyRelative(candidatePath);

            if (candidateProperty == null)
                continue;

            if (candidateProperty.propertyType != SerializedPropertyType.String)
                continue;

            if (string.IsNullOrWhiteSpace(candidateProperty.stringValue))
                continue;

            string sanitizedValue = candidateProperty.stringValue.Trim();
            string tokenName = ResolveStableTokenName(candidatePath);

            if (string.IsNullOrWhiteSpace(tokenName))
                continue;

            return string.Format("{0}:{1}", tokenName, sanitizedValue);
        }

        for (int candidateIndex = 0; candidateIndex < StableIntegerIdPropertyNames.Length; candidateIndex++)
        {
            string candidateName = StableIntegerIdPropertyNames[candidateIndex];
            SerializedProperty candidateProperty = arrayElementProperty.FindPropertyRelative(candidateName);

            if (candidateProperty == null)
                continue;

            if (candidateProperty.propertyType != SerializedPropertyType.Integer)
                continue;

            return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", candidateName, candidateProperty.intValue);
        }

        return string.Empty;
    }

    private static string ResolveStableTokenName(string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
            return string.Empty;

        int lastSeparatorIndex = propertyPath.LastIndexOf('.');

        if (lastSeparatorIndex < 0 || lastSeparatorIndex >= propertyPath.Length - 1)
            return propertyPath.Trim();

        return propertyPath.Substring(lastSeparatorIndex + 1).Trim();
    }

    /// <summary>
    /// Builds one stable array token that stores both current index and semantic ID.
    /// </summary>
    /// <param name="dataSegment">Original data segment in Unity format (for example data[3]).</param>
    /// <param name="stableToken">Semantic stable token in key:value form.</param>
    /// <returns>Combined segment with index fallback when available.</returns>
    private static string BuildStableDataSegmentWithFallbackIndex(string dataSegment, string stableToken)
    {
        if (string.IsNullOrWhiteSpace(stableToken))
            return dataSegment;

        if (!TryParseNumericArrayDataToken(dataSegment, out int indexValue))
            return string.Format("data[{0}]", stableToken);

        return string.Format(CultureInfo.InvariantCulture, "data[{0}|{1}]", indexValue, stableToken);
    }

    private static void ReplaceLastArrayToken(StringBuilder pathBuilder, string replacementToken)
    {
        if (pathBuilder == null)
            return;

        int lastDotIndex = pathBuilder.ToString().LastIndexOf('.');

        if (lastDotIndex < 0)
        {
            pathBuilder.Clear();
            pathBuilder.Append(replacementToken);
            return;
        }

        pathBuilder.Remove(lastDotIndex + 1, pathBuilder.Length - lastDotIndex - 1);
        pathBuilder.Append(replacementToken);
    }
    #endregion

    #endregion
}
#endif
