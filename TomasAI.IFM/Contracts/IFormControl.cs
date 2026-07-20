using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.Contracts
{
    public interface IFormControl
    {
        void Open();
        void Resize(Control parentControl);
        void Close();
    }

    public static class IControlExtension
    {
        public static void ShowErrorMessage(this Control view, string errorMsg, string caption)
        {
            try
            {
                view?.BeginInvoke((MethodInvoker)(() => MessageBox.Show(text: errorMsg, caption: caption, buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Error)));
            }
            catch { }

        }

        /// <summary>
        /// post onto ui thread of control to call suppiled action when thread is ready
        /// </summary>
        /// <param name="view"></param>
        /// <param name="viewAction"></param>
        public static void Post(this Control view, Action viewAction)
        {
            try
            {
                view?.BeginInvoke((MethodInvoker)(() => viewAction?.Invoke()));
            }
            catch { }
        }
           

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, Int32 wMsg, bool wParam, Int32 lParam);
        const int WM_SETREDRAW = 11;
        public static void Draw(this Control view, Action drawAction)
        {
            try
            {
                view?.BeginInvoke((MethodInvoker)(() =>
                {
                    // SuspendDrawing...
                    SendMessage(view.Handle, WM_SETREDRAW, false, 0);
                    drawAction();
                    // ResumeDrawing...
                    SendMessage(view.Handle, WM_SETREDRAW, true, 0);
                    view.Refresh();
                }));
            }
            catch { }
        }
    }
}
