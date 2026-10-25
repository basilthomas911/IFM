using System;
using System.Collections.Generic;
using System.Text;
using System.Data.Common;


namespace TomasAI.IFM.Shared.Storage
{
    public interface IObjectRepositoryTransaction
    {
        /// <summary>
        /// commit database transaction
        /// </summary>
        void Commit();

        /// <summary>
        /// rollback database transaction
        /// </summary>
        void Rollback();
    }

}
