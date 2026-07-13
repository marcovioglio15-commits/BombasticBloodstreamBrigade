using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;

/// <summary>
/// Resolves tokenized field bindings against current list contents while preserving deterministic legacy fallbacks.
/// </summary>
internal static class ExcelDataStableFieldBindingResolver
{
    #region Constants
    private const string TemplateListToken = ".Array.data[]";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves one field binding to its current concrete property without mutating the serialized owner.
    /// </summary>
    /// <param name="binding">Field identity containing a path template, stable keys and fallback indices.</param>
    /// <param name="serializedObject">Current serialized owner used for list-key inspection.</param>
    /// <param name="property">Resolved current property when successful.</param>
    /// <param name="resolvedPath">Current concrete Unity property path when successful.</param>
    /// <param name="warning">Blocking diagnostic when the binding cannot resolve unambiguously.</param>
    /// <returns>True when the binding identifies one current serialized property.</returns>
    public static bool TryResolveProperty(ExcelDataFieldBinding binding,
                                          SerializedObject serializedObject,
                                          out SerializedProperty property,
                                          out string resolvedPath,
                                          out string warning)
    {
        property = null;
        resolvedPath = string.Empty;
        warning = string.Empty;

        if (binding == null || serializedObject == null)
        {
            warning = "Stable field resolution requires both a binding and a serialized owner.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(binding.SerializedPath))
        {
            warning = "Binding has no concrete serialized property path.";
            return false;
        }

        string pathTemplate = binding.PathTemplate;

        if (string.IsNullOrWhiteSpace(pathTemplate))
        {
            if (HasAuthoredStableKey(binding.StableListKeys))
            {
                warning = "Binding contains stable list keys but has no tokenized path template. " +
                          "Numeric fallback was not used because the stable identity cannot be mapped safely.";
                return false;
            }

            return TryResolveConcretePath(serializedObject,
                                          binding.SerializedPath,
                                          out property,
                                          out resolvedPath,
                                          out warning);
        }

        if (pathTemplate.IndexOf(TemplateListToken, StringComparison.Ordinal) < 0)
        {
            if (HasAuthoredStableKey(binding.StableListKeys))
            {
                warning = "Binding contains stable list keys but its path template has no list tokens. " +
                          "Numeric fallback was not used because the stable identity cannot be mapped safely.";
                return false;
            }

            return TryResolveConcretePath(serializedObject,
                                          binding.SerializedPath,
                                          out property,
                                          out resolvedPath,
                                          out warning);
        }

        return TryResolveTemplate(binding,
                                  serializedObject,
                                  pathTemplate,
                                  out property,
                                  out resolvedPath,
                                  out warning);
    }
    #endregion

    #region Template Resolution
    /// <summary>
    /// Expands every list token using its stable key or explicit fallback index in nesting order.
    /// </summary>
    /// <param name="binding">Binding containing nested list identities.</param>
    /// <param name="serializedObject">Current serialized owner.</param>
    /// <param name="pathTemplate">Tokenized serialized path.</param>
    /// <param name="property">Resolved final property.</param>
    /// <param name="resolvedPath">Expanded current concrete path.</param>
    /// <param name="warning">Blocking resolution diagnostic.</param>
    /// <returns>True when all list scopes and the final property resolve.</returns>
    private static bool TryResolveTemplate(ExcelDataFieldBinding binding,
                                           SerializedObject serializedObject,
                                           string pathTemplate,
                                           out SerializedProperty property,
                                           out string resolvedPath,
                                           out string warning)
    {
        property = null;
        resolvedPath = string.Empty;
        warning = string.Empty;
        StringBuilder pathBuilder = new StringBuilder(pathTemplate.Length + 32);
        int copyStartIndex = 0;
        int listDepth = 0;
        int tokenIndex = pathTemplate.IndexOf(TemplateListToken, StringComparison.Ordinal);

        // Resolve each parent list before expanding the next nested path fragment.
        while (tokenIndex >= 0)
        {
            pathBuilder.Append(pathTemplate, copyStartIndex, tokenIndex - copyStartIndex);
            string listPropertyPath = pathBuilder.ToString();
            SerializedProperty listProperty = serializedObject.FindProperty(listPropertyPath);

            if (listProperty == null || !listProperty.isArray ||
                listProperty.propertyType == SerializedPropertyType.String)
            {
                warning = "List scope '" + listPropertyPath +
                          "' no longer resolves as a serialized list at nesting depth " +
                          (listDepth + 1).ToString(CultureInfo.InvariantCulture) + ".";
                return false;
            }

            int resolvedIndex;

            if (!TryResolveListElementIndex(binding,
                                            listProperty,
                                            listDepth,
                                            out resolvedIndex,
                                            out warning))
                return false;

            pathBuilder.Append(".Array.data[");
            pathBuilder.Append(resolvedIndex.ToString(CultureInfo.InvariantCulture));
            pathBuilder.Append(']');
            copyStartIndex = tokenIndex + TemplateListToken.Length;
            listDepth++;
            tokenIndex = pathTemplate.IndexOf(TemplateListToken, copyStartIndex, StringComparison.Ordinal);
        }

        if (HasAuthoredStableKeyAfterDepth(binding.StableListKeys, listDepth))
        {
            warning = "Binding contains more stable list keys than its path template exposes. " +
                      "Numeric fallback was not used because the identity metadata is inconsistent.";
            return false;
        }

        pathBuilder.Append(pathTemplate, copyStartIndex, pathTemplate.Length - copyStartIndex);
        resolvedPath = pathBuilder.ToString();
        property = serializedObject.FindProperty(resolvedPath);

        if (property != null)
            return true;

        warning = "Resolved serialized property no longer exists: " + resolvedPath + ".";
        resolvedPath = string.Empty;
        return false;
    }

    /// <summary>
    /// Resolves one list depth by stable key, or by the captured index only when no key was authored.
    /// </summary>
    /// <param name="binding">Binding containing nested stable keys and concrete indices.</param>
    /// <param name="listProperty">Current list scope.</param>
    /// <param name="listDepth">Zero-based nested list depth.</param>
    /// <param name="resolvedIndex">Current element index when successful.</param>
    /// <param name="warning">Blocking diagnostic for missing, duplicate or invalid identities.</param>
    /// <returns>True when one valid current list element is selected.</returns>
    private static bool TryResolveListElementIndex(ExcelDataFieldBinding binding,
                                                   SerializedProperty listProperty,
                                                   int listDepth,
                                                   out int resolvedIndex,
                                                   out string warning)
    {
        IReadOnlyList<string> stableKeys = binding.StableListKeys;
        string stableKey = listDepth < stableKeys.Count ? stableKeys[listDepth] : string.Empty;

        if (!string.IsNullOrWhiteSpace(stableKey))
            return ExcelDataListIdentityUtility.TryResolveUniqueElementIndex(listProperty,
                                                                             stableKey,
                                                                             out resolvedIndex,
                                                                             out warning);

        IReadOnlyList<int> concreteIndices = binding.ConcreteListIndices;

        if (listDepth >= concreteIndices.Count)
        {
            resolvedIndex = -1;
            warning = "Binding has neither a stable key nor a fallback index for list '" +
                      listProperty.propertyPath + "' at nesting depth " +
                      (listDepth + 1).ToString(CultureInfo.InvariantCulture) + ".";
            return false;
        }

        resolvedIndex = concreteIndices[listDepth];

        if (resolvedIndex >= 0 && resolvedIndex < listProperty.arraySize)
        {
            warning = string.Empty;
            return true;
        }

        warning = "Fallback index " + resolvedIndex.ToString(CultureInfo.InvariantCulture) +
                  " is outside list '" + listProperty.propertyPath + "' with " +
                  listProperty.arraySize.ToString(CultureInfo.InvariantCulture) + " elements.";
        resolvedIndex = -1;
        return false;
    }
    #endregion

    #region Fallback Resolution
    /// <summary>
    /// Resolves a non-tokenized or legacy binding through its stored concrete path.
    /// </summary>
    /// <param name="serializedObject">Current serialized owner.</param>
    /// <param name="concretePath">Stored concrete property path.</param>
    /// <param name="property">Resolved property.</param>
    /// <param name="resolvedPath">Concrete path when successful.</param>
    /// <param name="warning">Blocking diagnostic when the property is missing.</param>
    /// <returns>True when the concrete property still exists.</returns>
    private static bool TryResolveConcretePath(SerializedObject serializedObject,
                                               string concretePath,
                                               out SerializedProperty property,
                                               out string resolvedPath,
                                               out string warning)
    {
        property = serializedObject.FindProperty(concretePath);

        if (property != null)
        {
            resolvedPath = concretePath;
            warning = string.Empty;
            return true;
        }

        resolvedPath = string.Empty;
        warning = "Missing serialized property: " + concretePath + ".";
        return false;
    }
    #endregion

    #region Identity Validation
    /// <summary>
    /// Checks whether any nested list depth contains a non-empty authored key.
    /// </summary>
    /// <param name="stableKeys">Nested stable-key sequence.</param>
    /// <returns>True when at least one stable key is authored.</returns>
    private static bool HasAuthoredStableKey(IReadOnlyList<string> stableKeys)
    {
        return HasAuthoredStableKeyAfterDepth(stableKeys, 0);
    }

    /// <summary>
    /// Checks whether a stable key remains at or after one consumed list depth.
    /// </summary>
    /// <param name="stableKeys">Nested stable-key sequence.</param>
    /// <param name="startDepth">First zero-based depth to inspect.</param>
    /// <returns>True when a non-empty stable key remains.</returns>
    private static bool HasAuthoredStableKeyAfterDepth(IReadOnlyList<string> stableKeys, int startDepth)
    {
        if (stableKeys == null)
            return false;

        for (int keyIndex = startDepth; keyIndex < stableKeys.Count; keyIndex++)
        {
            if (!string.IsNullOrWhiteSpace(stableKeys[keyIndex]))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
