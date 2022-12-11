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
                model.OriginalFilePath = model.OriginalFilePath.Replace("/checks/", "/cpanel_whm/");
                model.DublicateFilePath = model.DublicateFilePath.Replace("/checks/", "/cpanel_whm/");
                model.WebmailFilePath = model.WebmailFilePath.Replace("/checks/", "/cpanel_whm/");
                model.CpanelGoodFilePath = model.CpanelGoodFilePath.Replace("/checks/", "/cpanel_whm/");
                model.CpanelBadFilePath = model.CpanelBadFilePath.Replace("/checks/", "/cpanel_whm/");
                model.WhmGoodFilePath = model.WhmGoodFilePath.Replace("/checks/", "/cpanel_whm/");
                model.WhmBadFilePath = model.WhmBadFilePath.Replace("/checks/", "/cpanel_whm/");
                await CpanelWhmCheckController.PutCheckAsync(model, null);
            }
            Console.WriteLine("Done");
        }
    }
}