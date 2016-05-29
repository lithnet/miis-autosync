using System;

namespace Lithnet.Miiserver.AutoSync
{
    public sealed class CallbackEventBridge
    {
        public event AsyncCallback CallbackComplete = delegate { };

        private CallbackEventBridge()
        {
        }

        private void CallbackInternal(IAsyncResult result)
        {
            this.CallbackComplete(result);
        }

        public AsyncCallback Callback => this.CallbackInternal;

        public static CallbackEventBridge Create()
        {
            return new CallbackEventBridge();
        }
    }
}