namespace Services.Interfaces
{
    public interface IMemorySaver
    {
        /// <summary>
        /// Добавить в кэш элемент
        /// </summary>
        /// <typeparam name="TData">Тип элемента</typeparam>
        /// <param name="alias">Алиас</param>
        /// <param name="item">Элемент</param>
        void StoreItem<TData>(string alias, TData item);

        /// <summary>
        /// Получить элемент из кэша
        /// </summary>
        /// <typeparam name="TData">Тип элемента</typeparam>
        /// <param name="alias">Алиас</param>
        /// <returns>Элемент с указанным типом</returns>
        TData GetItem<TData>(string alias);

        /// <summary>
        /// Проверить наличие элемента в кэше
        /// </summary>
        /// <param name="alias">Алиас</param>
        /// <returns>True если присутствует</returns>
        bool ItemExist(string alias);

        /// <summary>
        /// Удалить элемент из кэша
        /// </summary>
        /// <param name="alias">Алиас</param>
        void RemoveItem(string alias);
    }
}