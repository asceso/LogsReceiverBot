using Models.App;
using Newtonsoft.Json;
using Services.Interfaces;
using System.Collections.ObjectModel;

namespace Services.Implementation
{
    public class JsonAdapter : IJsonAdapter
    {
        public JsonAdapter()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        public ConfigModel ReadJsonConfig()
        {
            using StreamReader reader = new(PathCollection.ConfigPath);
            string configData = reader.ReadToEnd();
            reader.Close();
            return JsonConvert.DeserializeObject<ConfigModel>(configData);
        }

        public ObservableCollection<OperationModel> ReadJsonOperations()
        {
            using StreamReader reader = new(PathCollection.OperationsPath);
            string operationsData = reader.ReadToEnd();
            reader.Close();
            ObservableCollection<OperationModel> operations = JsonConvert.DeserializeObject<ObservableCollection<OperationModel>>(operationsData);
            operations.CollectionChanged += (s, e) =>
            {
                using StreamWriter writer = new(PathCollection.OperationsPath);
                string buffer = JsonConvert.SerializeObject(operations, Formatting.Indented);
                writer.Write(buffer);
                writer.Close();
            };
            return operations;
        }

        public List<LocaleStringModel> ReadJsonLocaleStrings()
        {
            using StreamReader reader = new(PathCollection.LocalesPath);
            string localesData = reader.ReadToEnd();
            reader.Close();
            return JsonConvert.DeserializeObject<List<LocaleStringModel>>(localesData);
        }
    }
}