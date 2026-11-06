using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// This class is used to set the target frame rate of the application based on keyboard input,
/// useful for catching performance issues in the game.
/// </summary>
public class FrameRateLimiter : MonoBehaviour {
    void Update() {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (!keyboard.leftShiftKey.isPressed) return;
        if (keyboard.f1Key.wasPressedThisFrame) Application.targetFrameRate = 10;
        if (keyboard.f2Key.wasPressedThisFrame) Application.targetFrameRate = 20;
        if (keyboard.f3Key.wasPressedThisFrame) Application.targetFrameRate = 30;
        if (keyboard.f4Key.wasPressedThisFrame) Application.targetFrameRate = 60;
        if (keyboard.f5Key.wasPressedThisFrame) Application.targetFrameRate = 900;
    }
}
