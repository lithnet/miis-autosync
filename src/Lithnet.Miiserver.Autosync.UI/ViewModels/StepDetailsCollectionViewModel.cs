using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Data;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class StepDetailsCollectionViewModel : ListViewModel<StepDetailsViewModel, StepDetails>
    {
        public StepDetailsCollectionViewModel(IList<StepDetails> items, Func<Guid, string, IEnumerable<CSObjectRef>> stepDetailsFunc)
            : base((IList<StepDetails>)items, t => StepDetailsCollectionViewModel.ViewModelResolver(t, stepDetailsFunc))
        {
            if (this.ViewModels.Count > 0)
            {
                this.ViewModels[0].IsSelected = true;
            }
        }

        private static StepDetailsViewModel ViewModelResolver(StepDetails model, Func<Guid, string, IEnumerable<CSObjectRef>> stepDetailsFunc)
        { 
            return new StepDetailsViewModel(model, stepDetailsFunc);
        }
    }
}
