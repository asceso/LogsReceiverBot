namespace Extensions
{
    public static class ListExtension
    {
        public static List<List<TData>> Partitions<TData>(this List<TData> source, int partitionsCount)
        {
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
    }
}