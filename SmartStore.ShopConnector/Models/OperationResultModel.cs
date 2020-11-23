using System;

namespace SmartStore.ShopConnector.Models
{
    [Serializable]
    public class OperationResultModel
    {
        public OperationResultModel()
        {
        }
        public OperationResultModel(string message, bool hasError = true)
        {
            HasError = hasError;
            ShortMessage = message;
        }
        public OperationResultModel(Exception exception)
        {
            HasError = true;
            ShortMessage = exception.ToAllMessages();
            Description = exception.ToString();
        }

        public bool HasError { get; set; }
        public string ShortMessage { get; set; }
        public string Description { get; set; }
    }
}
