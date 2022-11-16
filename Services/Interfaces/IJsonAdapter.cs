using Models.App;
using System.Collections.ObjectModel;

namespace Services.Interfaces
{
    public interface IJsonAdapter
    {
        public ConfigModel ReadJsonConfig();

        public ObservableCollection<OperationModel> ReadJsonOperations();

        public List<LocaleStringModel> ReadJsonLocaleStrings();
    }
}