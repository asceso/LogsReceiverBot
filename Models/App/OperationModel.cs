using Models.Enums;

namespace Models.App
{
    public class OperationModel
    {
        public long UserId { get; set; }
        public OperationType OperationType { get; set; }
        public Dictionary<string, object> Params { get; set; }

        public OperationModel(long userId, OperationType operationType, params KeyValuePair<string, object>[] operationParams)
        {
            UserId = userId;
            OperationType = operationType;
            Params = new Dictionary<string, object>();
            if (operationParams is not null)
            {
                foreach (var pair in operationParams)
                {
                    try
                    {
                        Params.Add(pair.Key, pair.Value);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
        }
    }
}