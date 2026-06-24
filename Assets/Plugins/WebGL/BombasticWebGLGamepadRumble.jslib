mergeInto(LibraryManager.library, {
  BombasticWebGLGamepadSetRumble: function (lowFrequency, highFrequency, durationMilliseconds) {
    if (!navigator.getGamepads) {
      return;
    }

    var gamepads;

    try {
      gamepads = navigator.getGamepads();
    } catch (error) {
      return;
    }
    var selectedActuator = null;

    for (var gamepadIndex = 0; gamepadIndex < gamepads.length; gamepadIndex++) {
      var gamepad = gamepads[gamepadIndex];

      if (!gamepad || !gamepad.connected) {
        continue;
      }

      var actuator = gamepad.vibrationActuator;

      if (!actuator && gamepad.hapticActuators && gamepad.hapticActuators.length > 0) {
        actuator = gamepad.hapticActuators[0];
      }

      if (!actuator) {
        continue;
      }

      if (!selectedActuator) {
        selectedActuator = actuator;
      }

      var isActiveGamepad = false;

      for (var buttonIndex = 0; buttonIndex < gamepad.buttons.length; buttonIndex++) {
        var button = gamepad.buttons[buttonIndex];

        if (button && (button.pressed || button.value > 0.15)) {
          isActiveGamepad = true;
          break;
        }
      }

      if (!isActiveGamepad) {
        for (var axisIndex = 0; axisIndex < gamepad.axes.length; axisIndex++) {
          if (Math.abs(gamepad.axes[axisIndex]) > 0.2) {
            isActiveGamepad = true;
            break;
          }
        }
      }

      if (isActiveGamepad) {
        selectedActuator = actuator;
        break;
      }
    }

    if (!selectedActuator) {
      return;
    }

    var duration = Math.max(1, durationMilliseconds | 0);
    var strongMagnitude = Math.max(0, Math.min(1, lowFrequency));
    var weakMagnitude = Math.max(0, Math.min(1, highFrequency));

    if (selectedActuator.playEffect) {
      var effectResult;

      try {
        effectResult = selectedActuator.playEffect("dual-rumble", {
          startDelay: 0,
          duration: duration,
          strongMagnitude: strongMagnitude,
          weakMagnitude: weakMagnitude
        });
      } catch (error) {
        return;
      }

      if (effectResult && effectResult.catch) {
        effectResult.catch(function () {});
      }

      return;
    }

    if (selectedActuator.pulse) {
      var pulseResult;

      try {
        pulseResult = selectedActuator.pulse(Math.max(strongMagnitude, weakMagnitude), duration);
      } catch (error) {
        return;
      }

      if (pulseResult && pulseResult.catch) {
        pulseResult.catch(function () {});
      }
    }
  },

  BombasticWebGLGamepadResetRumble: function () {
    if (!navigator.getGamepads) {
      return;
    }

    var gamepads;

    try {
      gamepads = navigator.getGamepads();
    } catch (error) {
      return;
    }

    for (var gamepadIndex = 0; gamepadIndex < gamepads.length; gamepadIndex++) {
      var gamepad = gamepads[gamepadIndex];

      if (!gamepad || !gamepad.connected) {
        continue;
      }

      var actuator = gamepad.vibrationActuator;

      if (!actuator && gamepad.hapticActuators && gamepad.hapticActuators.length > 0) {
        actuator = gamepad.hapticActuators[0];
      }

      if (!actuator) {
        continue;
      }

      if (actuator.reset) {
        var resetResult;

        try {
          resetResult = actuator.reset();
        } catch (error) {
          continue;
        }

        if (resetResult && resetResult.catch) {
          resetResult.catch(function () {});
        }

        continue;
      }

      if (actuator.playEffect) {
        var stopResult;

        try {
          stopResult = actuator.playEffect("dual-rumble", {
            startDelay: 0,
            duration: 1,
            strongMagnitude: 0,
            weakMagnitude: 0
          });
        } catch (error) {
          continue;
        }

        if (stopResult && stopResult.catch) {
          stopResult.catch(function () {});
        }
      }
    }
  }
});
