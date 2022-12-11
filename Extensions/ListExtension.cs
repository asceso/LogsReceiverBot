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

        public static List<List<TData>> Split<TData>(this List<TData> source, int splitCount)
        {
            List<List<TData>> split = new();
            if (source.Count <= splitCount)
            {
                split.Add(source);
                return split;
            }
            int partCount = (int)source.Count / splitCount;
            if (splitCount * partCount < source.Count)
            {
                partCount++;
            }
            for (int i = 0; i < partCount; i++)
            {
                split.Add(source.Skip(i * splitCount).Take(splitCount).ToList());
            }
            return split;
        }

        public static async Task SaveToFile(this List<string> source, string filename)
        {
            using StreamWriter writer = new(filename);
            for (int i = 0; i < source.Count; i++)
            {
                if (i == source.Count - 1)
                {
                    await writer.WriteAsync(source[i]);
                }
                else
                {
                    await writer.WriteLineAsync(source[i]);
                }
            }
            writer.Close();
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