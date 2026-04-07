using lamat.Models;

// Used to decide which key to highlight.

namespace lamat.Services
{
    public static class ModifierKeyIds
    {
        public static bool IsModifier(string keyId) =>
            keyId is "LeftShift" or "RightShift" or
                     "LeftCtrl" or "RightCtrl" or
                     "LeftAlt" or "RightAlt";
    }

    public class KeyboardHintService
    {
        // Returns the hint text to display for the current step.
        // currentStep: the step the user needs to complete now
        // modifierHeld: true if a modifier (Shift/Ctrl/Alt) from a previous step is still physically held
        public string GetHintText(KeyStep? currentStep, bool modifierHeld)
        {
            if (currentStep == null) return "";

            string keyId = currentStep.KeyId;

            if (ModifierKeyIds.IsModifier(keyId))
                return FormatModifier(keyId);

            return keyId;
        }

        // Returns which keys the visual keyboard should highlight.
        // Returns two entries when a modifier is held: [modifier, key]
        // Returns one entry otherwise.
        public string[] GetKeysToHighlight(KeyStep? currentStep, string? heldModifier)
        {
            if (currentStep == null) return [];

            if (heldModifier != null && !ModifierKeyIds.IsModifier(currentStep.KeyId))
                return [heldModifier, currentStep.KeyId];

            return [currentStep.KeyId];
        }

        private static string FormatModifier(string keyId) => keyId switch
        {
            "LeftShift" or "RightShift" => "Shift",
            "LeftCtrl"  or "RightCtrl"  => "Ctrl",
            "LeftAlt"   or "RightAlt"   => "Alt",
            _ => keyId
        };
    }
}
