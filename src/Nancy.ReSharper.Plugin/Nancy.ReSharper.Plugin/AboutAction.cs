using System.Windows.Forms;
using JetBrains.ActionManagement;
using JetBrains.Application.DataContext;

namespace Nancy.ReSharper.Plugin
{
  [ActionHandler("Nancy.ReSharper.Plugin.About")]
  public class AboutAction : IActionHandler
  {
    public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
    {
      // return true or false to enable/disable this action
      return true;
    }

    public void Execute(IDataContext context, DelegateExecute nextExecute)
    {
      MessageBox.Show(
        "Nancy ReSharper Plugin\nIgal Tabachnik\n\nAdds support for NancyFX in ReSharper",
        "About Nancy ReSharper Plugin",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);
    }
  }
}
