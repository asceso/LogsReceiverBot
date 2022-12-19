using DataAdapter.Controllers;
using Models.Database;

namespace DatabasePathUpdater
{
    public class Program
    {
        private static async Task Main()
        {
            List<CpanelWhmCheckModel> models = await CpanelWhmCheckController.GetChecksAsync();
            foreach (CpanelWhmCheckModel model in models)
            {
                model.OriginalFilePath = model.OriginalFilePath.Replace("\\admin\\", "\\Administrator\\");
                model.DublicateFilePath = model.DublicateFilePath.Replace("\\admin\\", "\\Administrator\\");
                model.WebmailFilePath = model.WebmailFilePath.Replace("\\admin\\", "\\Administrator\\");
                model.CpanelGoodFilePath = model.CpanelGoodFilePath.Replace("\\admin\\", "\\Administrator\\");
                model.CpanelBadFilePath = model.CpanelBadFilePath.Replace("\\admin\\", "\\Administrator\\");
                model.WhmGoodFilePath = model.WhmGoodFilePath.Replace("\\admin\\", "\\Administrator\\");
                model.WhmBadFilePath = model.WhmBadFilePath.Replace("\\admin\\", "\\Administrator\\");
                await CpanelWhmCheckController.PutCheckAsync(model, null);
                Console.WriteLine("Changed cpanel check id #" + model.Id);
            }
            List<WpLoginCheckModel> wpModels = await WpLoginCheckController.GetChecksAsync();
            foreach (WpLoginCheckModel model in wpModels)
            {
                model.OriginalFilePath = model.OriginalFilePath.Replace("\\admin\\", "\\Administrator\\");
                model.DublicateFilePath = model.DublicateFilePath.Replace("\\admin\\", "\\Administrator\\");
                model.ShellsFilePath = model.ShellsFilePath.Replace("\\admin\\", "\\Administrator\\");
                model.CpanelsFilePath = model.CpanelsFilePath.Replace("\\admin\\", "\\Administrator\\");
                model.SmtpsFilePath = model.SmtpsFilePath.Replace("\\admin\\", "\\Administrator\\");
                model.LoggedWordpressFilePath = model.LoggedWordpressFilePath.Replace("\\admin\\", "\\Administrator\\");
                await WpLoginCheckController.PutCheckAsync(model, null);
                Console.WriteLine("Changed wp-login check id #" + model.Id);
            }
            Console.WriteLine("Done");
            Console.ReadKey();
        }
    }
}