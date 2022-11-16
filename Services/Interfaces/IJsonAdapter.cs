using Models.App;
using System.Collections.ObjectModel;

namespace Services.Interfaces
{
    public interface IJsonAdapter
    {
        public Task<ConfigModel> ReadJsonConfigAsync();

        public Task SaveJsonConfigAsync(ConfigModel config);

        public Task<ObservableCollection<OperationModel>> ReadJsonOperationsAsync();

        public Task<List<LocaleStringModel>> ReadJsonLocaleStringsAsync();
    }
}