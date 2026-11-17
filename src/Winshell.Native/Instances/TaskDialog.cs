using Windows.Win32;
using Windows.Win32.UI.Controls;

namespace Winshell.Native;

internal static unsafe class TaskDialog
{
    private const int Ok = 1;

    internal static int? ShowError(string title, string instruction, string content, (int Id, string Text)? button)
    {
        fixed (char* titleText = title, instructionText = instruction, contentText = content, buttonText = button?.Text)
        {
            var custom = new TASKDIALOG_BUTTON { nButtonID = button?.Id ?? 0, pszButtonText = buttonText };
            var config = new TASKDIALOGCONFIG
            {
                cbSize = (uint)sizeof(TASKDIALOGCONFIG),
                dwFlags = TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION | TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT,
                dwCommonButtons = TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON,
                pszWindowTitle = titleText,
                pszMainIcon = PInvoke.TD_ERROR_ICON,
                pszMainInstruction = instructionText,
                pszContent = contentText,
                cButtons = button is null ? 0u : 1u,
                pButtons = button is null ? null : &custom,
                nDefaultButton = Ok
            };
            int pressed;
            return PInvoke.TaskDialogIndirect(&config, &pressed, null, null).Succeeded ? pressed : null;
        }
    }
}
