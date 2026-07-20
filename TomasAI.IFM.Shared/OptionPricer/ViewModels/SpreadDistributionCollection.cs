using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TomasAI.IFM.Shared.OptionPricer.ViewModels
{
    public class SpreadDistributionCollection : ICollection<SpreadDistributionReadModel>
    {
        private List<SpreadDistributionReadModel> _spreadDistributions;

        public SpreadDistributionCollection()
        {
            _spreadDistributions = new List<SpreadDistributionReadModel>();
        }

        public int Count => _spreadDistributions.Count;

        public bool IsReadOnly => false;

        public void Add(SpreadDistributionReadModel spreadPathProjection)
            => _spreadDistributions.Add(spreadPathProjection);

        public void Clear()
            => _spreadDistributions.Clear();

        public bool Contains(SpreadDistributionReadModel spreadPathProjection)
            => _spreadDistributions.Contains(spreadPathProjection);

        public void CopyTo(SpreadDistributionReadModel[] array, int arrayIndex)
            => _spreadDistributions.CopyTo(array, arrayIndex);

        public IEnumerator<SpreadDistributionReadModel> GetEnumerator()
            => _spreadDistributions.GetEnumerator();

        public bool Remove(SpreadDistributionReadModel spreadPathProjection)
            => _spreadDistributions.Remove(spreadPathProjection);

        IEnumerator IEnumerable.GetEnumerator()
            => _spreadDistributions.GetEnumerator();
    }
}
