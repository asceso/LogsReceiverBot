using Models.App;
using Newtonsoft.Json;
using Services.Interfaces;
using System.Collections.ObjectModel;

namespace Services.Implementation
{
    public class JsonAdapter : IJsonAdapter
    {
        public async Task<ConfigModel> ReadJsonConfigAsync()
        {
            if (!File.Exists(PathCollection.ConfigPath))
            {
                await SaveJsonConfigAsync(new());
            }
            using StreamReader reader = new(PathCollection.ConfigPath);
            string configData = await reader.ReadToEndAsync();
            reader.Close();
            return JsonConvert.DeserializeObject<ConfigModel>(configData);
        }

        public async Task SaveJsonConfigAsync(ConfigModel config)
        {
            using StreamWriter writer = new(PathCollection.ConfigPath);
            await writer.WriteAsync(JsonConvert.SerializeObject(config, Formatting.Indented));
            await writer.FlushAsync();
            writer.Close();
        }

        public async Task<ObservableCollection<OperationModel>> ReadJsonOperationsAsync()
        {
            if (!File.Exists(PathCollection.OperationsPath))
            {
                using StreamWriter writer = new(PathCollection.OperationsPath);
                await writer.WriteAsync("[]");
                await writer.FlushAsync();
                writer.Close();
            }

            using StreamReader reader = new(PathCollection.OperationsPath);
            string operationsData = await reader.ReadToEndAsync();
            reader.Close();
            ObservableCollection<OperationModel> operations = JsonConvert.DeserializeObject<ObservableCollection<OperationModel>>(operationsData);
            operations.CollectionChanged += async (s, e) =>
            {
                using StreamWriter writer = new(PathCollection.OperationsPath);
                string buffer = JsonConvert.SerializeObject(operations, Formatting.Indented);
                await writer.WriteAsync(buffer);
                await writer.FlushAsync();
                writer.Close();
            };
            return operations;
        }

        public async Task<List<LocaleStringModel>> ReadJsonLocaleStringsAsync()
        {
            if (!File.Exists(PathCollection.LocalesPath))
            {
                using StreamWriter writer = new(PathCollection.LocalesPath);
                await writer.WriteAsync("[]");
                await writer.FlushAsync();
                writer.Close();
            }

            using StreamReader reader = new(PathCollection.LocalesPath);
            string localesData = await reader.ReadToEndAsync();
            reader.Close();
            return JsonConvert.DeserializeObject<List<LocaleStringModel>>(localesData);
        }
    }
}