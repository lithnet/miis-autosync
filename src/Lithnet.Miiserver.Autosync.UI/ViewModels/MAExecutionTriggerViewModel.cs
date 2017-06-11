using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public abstract class MAExecutionTriggerViewModel : ViewModelBase<IMAExecutionTrigger>
    {
        protected MAExecutionTriggerViewModel(IMAExecutionTrigger model)
            : base(model)
        {
        }
    }
}
