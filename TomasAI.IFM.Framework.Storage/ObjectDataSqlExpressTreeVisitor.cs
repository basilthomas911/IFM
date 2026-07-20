using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Storage
{
    public class ObjectDataSqlExpressTreeVisitor : LinqExpressionTreeVisitor
    {
        private string _sqlText = string.Empty;

        /// <summary>
        /// create linq expression tree visitor that will convert expression
        /// into equivalent sql code with in limits
        /// </summary>
        /// <param name="expressionTree"></param>
        public ObjectDataSqlExpressTreeVisitor(Expression expressionTree)
        {
            this.Visit(expressionTree);
        }

        /// <summary>
        /// return generated sql code from traversing expression tree
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _sqlText;
        }
    }
}
