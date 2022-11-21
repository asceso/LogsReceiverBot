using System.Collections.ObjectModel;

namespace Extensions
{
    public static class ListExtension
    {
        public static List<List<TData>> Partitions<TData>(this List<TData> source, int partitionsCount)
        {
            if (partitionsCount == 0)
            {
                partitionsCount = 1;
            }
            int currentThread = 0;
            List<List<TData>> partitions = new();
            for (int i = 0; i < partitionsCount; i++)
            {
                partitions.Add(new());
            }
            for (int i = 0; i < source.Count; i++)
            {
                if (currentThread >= partitionsCount)
                {
                    currentThread = 0;
                }
                partitions[currentThread].Add(source[i]);
                currentThread++;
            }
            return partitions;
        }

        public static TData GetRandom<TData>(this ICollection<TData> source)
        {
            int randomIndex = Random.Shared.Next(source.Count);
            if (source is List<TData> ld)
            {
                return ld[randomIndex];
            }
            if (source is ObservableCollection<TData> od)
            {
                return od[randomIndex];
            }
            return source.ElementAt(randomIndex);
        }
    }
}