using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage
{
    public abstract class LinqExpressionTreeVisitor
    {
        private Dictionary<ExpressionType, Func<Expression, Expression>> _exprNodeMap;

        protected LinqExpressionTreeVisitor()
        {

            _exprNodeMap = new Dictionary<ExpressionType, Func<Expression, Expression>>
            {
                // unary expressions...
                {ExpressionType.Negate, e => VisitUnary((dynamic)e) },
                {ExpressionType.NegateChecked, e => VisitUnary((dynamic)e) },
                {ExpressionType.Not, e => VisitUnary((dynamic)e) },
                {ExpressionType.Convert, e => VisitUnary((dynamic)e) },
                {ExpressionType.ConvertChecked, e => VisitUnary((dynamic)e) },
                {ExpressionType.ArrayLength, e => VisitUnary((dynamic)e) },
                {ExpressionType.Quote, e => VisitUnary((dynamic)e) },
                {ExpressionType.TypeAs, e => VisitUnary((dynamic)e) },

                // binary expressions...
                {ExpressionType.Add, e => VisitBinary((dynamic)e) },
                {ExpressionType.AddChecked, e => VisitBinary((dynamic)e) },
                {ExpressionType.Subtract, e => VisitBinary((dynamic)e) },
                {ExpressionType.SubtractChecked, e => VisitBinary((dynamic)e) },
                {ExpressionType.Multiply, e => VisitBinary((dynamic)e) },
                {ExpressionType.MultiplyChecked, e => VisitBinary((dynamic)e) },
                {ExpressionType.Divide, e => VisitBinary((dynamic)e) },
                {ExpressionType.Modulo, e => VisitBinary((dynamic)e) },
                {ExpressionType.And, e => VisitBinary((dynamic)e) },
                {ExpressionType.AndAlso, e => VisitBinary((dynamic)e) },
                {ExpressionType.Or, e => VisitBinary((dynamic)e) },
                {ExpressionType.OrElse, e => VisitBinary((dynamic)e) },
                {ExpressionType.LessThan, e => VisitBinary((dynamic)e) },
                {ExpressionType.LessThanOrEqual, e => VisitBinary((dynamic)e) },
                {ExpressionType.GreaterThan, e => VisitBinary((dynamic)e) },
                {ExpressionType.GreaterThanOrEqual, e => VisitBinary((dynamic)e) },
                {ExpressionType.Equal, e => VisitBinary((dynamic)e) },
                {ExpressionType.NotEqual, e => VisitBinary((dynamic)e) },
                {ExpressionType.Coalesce, e => VisitBinary((dynamic)e) },
                {ExpressionType.ArrayIndex, e => VisitBinary((dynamic)e) },
                {ExpressionType.RightShift, e => VisitBinary((dynamic)e) },
                {ExpressionType.ExclusiveOr, e => VisitBinary((dynamic)e) },

                // all other expressions...
                {ExpressionType.TypeIs, e => VisitTypeIs((dynamic)e) },
                {ExpressionType.Conditional, e => VisitConditional((dynamic)e) },
                {ExpressionType.Constant, e => VisitConstant((dynamic)e) },
                {ExpressionType.Parameter, e => VisitParameter((dynamic)e) },
                {ExpressionType.MemberAccess, e => VisitMemberAccess((dynamic)e) },
                {ExpressionType.Call, e => VisitMethodCall((dynamic)e) },
                {ExpressionType.Lambda, e => VisitLambda((dynamic)e) },
                {ExpressionType.New, e => VisitNew((dynamic)e) },
                {ExpressionType.NewArrayInit, e => VisitNewArray((dynamic)e) },
                {ExpressionType.NewArrayBounds, e => VisitNewArray((dynamic)e) },
                {ExpressionType.Invoke, e => VisitInvocation((dynamic)e) },
                {ExpressionType.MemberInit, e => VisitMemberInit((dynamic)e) },
                {ExpressionType.ListInit, e => VisitListInit((dynamic)e) }
            };

        }

        /// <summary>
        /// visit each node in expression tree
        /// </summary>
        /// <param name="expr">linq expression tree</param>
        /// <returns></returns>
        protected virtual Expression Visit(Expression expressionTree)
        {
            if (expressionTree == null) return expressionTree;
            var exprNodeType = expressionTree.NodeType;
            if (!_exprNodeMap.ContainsKey(exprNodeType))
                throw new InvalidOperationException($"LinqExpressionTreeVisitor.Visit: expression '{exprNodeType}' is not mapped");
            return _exprNodeMap[expressionTree.NodeType](expressionTree);
        }

        /// <summary>
        /// visit unary node expression
        /// </summary>
        /// <param name="u">unary expression</param>
        /// <returns></returns>
        protected virtual Expression VisitUnary(UnaryExpression u)
        {
            Expression operand = this.Visit(u.Operand);
            if (operand != u.Operand)
            {
                return Expression.MakeUnary(u.NodeType, operand, u.Type, u.Method);
            }
            return u;
        }

        /// <summary>
        /// visit binary node expression
        /// </summary>
        /// <param name="b">binary node expression</param>
        /// <returns></returns>
        protected virtual Expression VisitBinary(BinaryExpression b)
        {
            Expression left = this.Visit(b.Left);
            Expression right = this.Visit(b.Right);
            Expression conversion = this.Visit(b.Conversion);
            if (left != b.Left || right != b.Right || conversion != b.Conversion)
            {
                if (b.NodeType == ExpressionType.Coalesce && b.Conversion != null)
                    return Expression.Coalesce(left, right, conversion as LambdaExpression);
                else
                    return Expression.MakeBinary(b.NodeType, left, right, b.IsLiftedToNull, b.Method);
            }
            return b;
        }

        protected virtual Expression VisitTypeIs(TypeBinaryExpression b)
        {
            Expression expr = this.Visit(b.Expression);
            if (expr != b.Expression)
            {
                return Expression.TypeIs(expr, b.TypeOperand);
            }
            return b;
        }

        protected virtual Expression VisitConstant(ConstantExpression c)
        {
            return c;
        }

        protected virtual Expression VisitConditional(ConditionalExpression c)
        {
            Expression test = this.Visit(c.Test);
            Expression ifTrue = this.Visit(c.IfTrue);
            Expression ifFalse = this.Visit(c.IfFalse);
            if (test != c.Test || ifTrue != c.IfTrue || ifFalse != c.IfFalse)
            {
                return Expression.Condition(test, ifTrue, ifFalse);
            }
            return c;
        }

        protected virtual Expression VisitParameter(ParameterExpression p)
        {
            return p;
        }

        protected virtual Expression VisitMemberAccess(MemberExpression m)
        {
            Expression exp = this.Visit(m.Expression);
            if (exp != m.Expression)
            {
                return Expression.MakeMemberAccess(exp, m.Member);
            }
            return m;
        }

        protected virtual Expression VisitMethodCall(MethodCallExpression m)
        {
            Expression obj = this.Visit(m.Object);
            IEnumerable<Expression> args = this.VisitExpressionList(m.Arguments);
            if (obj != m.Object || args != m.Arguments)
            {
                return Expression.Call(obj, m.Method, args);
            }
            return m;
        }

        protected virtual Expression VisitLambda(LambdaExpression lambda)
        {
            Expression body = this.Visit(lambda.Body);
            if (body != lambda.Body)
            {
                return Expression.Lambda(lambda.Type, body, lambda.Parameters);
            }
            return lambda;
        }

        protected virtual Expression VisitMemberInit(MemberInitExpression init)
        {
            NewExpression n = this.VisitNew(init.NewExpression);
            IEnumerable<MemberBinding> bindings = this.VisitBindingList(init.Bindings);
            if (n != init.NewExpression || bindings != init.Bindings)
            {
                return Expression.MemberInit(n, bindings);
            }
            return init;
        }

        protected virtual Expression VisitListInit(ListInitExpression init)
        {
            NewExpression n = this.VisitNew(init.NewExpression);
            IEnumerable<ElementInit> initializers = this.VisitElementInitializerList(init.Initializers);
            if (n != init.NewExpression || initializers != init.Initializers)
            {
                return Expression.ListInit(n, initializers);
            }
            return init;
        }

        protected virtual Expression VisitNewArray(NewArrayExpression na)
        {
            IEnumerable<Expression> exprs = this.VisitExpressionList(na.Expressions);
            if (exprs != na.Expressions)
            {
                if (na.NodeType == ExpressionType.NewArrayInit)
                {
                    return Expression.NewArrayInit(na.Type.GetElementType(), exprs);
                }
                else
                {
                    return Expression.NewArrayBounds(na.Type.GetElementType(), exprs);
                }
            }
            return na;
        }

        protected virtual Expression VisitInvocation(InvocationExpression iv)
        {
            IEnumerable<Expression> args = this.VisitExpressionList(iv.Arguments);
            Expression expr = this.Visit(iv.Expression);
            if (args != iv.Arguments || expr != iv.Expression)
            {
                return Expression.Invoke(expr, args);
            }
            return iv;
        }

        protected virtual NewExpression VisitNew(NewExpression nex)
        {
            IEnumerable<Expression> args = this.VisitExpressionList(nex.Arguments);
            if (args != nex.Arguments)
            {
                if (nex.Members != null)
                    return Expression.New(nex.Constructor, args, nex.Members);
                else
                    return Expression.New(nex.Constructor, args);
            }
            return nex;
        }


        protected virtual MemberBinding VisitBinding(MemberBinding binding)
        {
            switch (binding.BindingType)
            {
                case MemberBindingType.Assignment:
                    return this.VisitMemberAssignment((MemberAssignment)binding);
                case MemberBindingType.MemberBinding:
                    return this.VisitMemberMemberBinding((MemberMemberBinding)binding);
                case MemberBindingType.ListBinding:
                    return this.VisitMemberListBinding((MemberListBinding)binding);
                default:
                    throw new Exception(string.Format("Unhandled binding type '{0}'", binding.BindingType));
            }
        }

        protected virtual ElementInit VisitElementInitializer(ElementInit initializer)
        {
            ReadOnlyCollection<Expression> arguments = this.VisitExpressionList(initializer.Arguments);
            if (arguments != initializer.Arguments)
            {
                return Expression.ElementInit(initializer.AddMethod, arguments);
            }
            return initializer;
        }

        protected virtual MemberAssignment VisitMemberAssignment(MemberAssignment assignment)
        {
            Expression e = this.Visit(assignment.Expression);
            if (e != assignment.Expression)
            {
                return Expression.Bind(assignment.Member, e);
            }
            return assignment;
        }

        protected virtual MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding binding)
        {
            IEnumerable<MemberBinding> bindings = this.VisitBindingList(binding.Bindings);
            if (bindings != binding.Bindings)
            {
                return Expression.MemberBind(binding.Member, bindings);
            }
            return binding;
        }

        protected virtual MemberListBinding VisitMemberListBinding(MemberListBinding binding)
        {
            IEnumerable<ElementInit> initializers = this.VisitElementInitializerList(binding.Initializers);
            if (initializers != binding.Initializers)
            {
                return Expression.ListBind(binding.Member, initializers);
            }
            return binding;
        }

        protected virtual IEnumerable<MemberBinding> VisitBindingList(ReadOnlyCollection<MemberBinding> original)
        {
            List<MemberBinding> list = null;
            for (int i = 0, n = original.Count; i < n; i++)
            {
                MemberBinding b = this.VisitBinding(original[i]);
                if (list != null)
                {
                    list.Add(b);
                }
                else if (b != original[i])
                {
                    list = new List<MemberBinding>(n);
                    for (int j = 0; j < i; j++)
                    {
                        list.Add(original[j]);
                    }
                    list.Add(b);
                }
            }
            if (list != null)
                return list;
            return original;
        }

        protected virtual IEnumerable<ElementInit> VisitElementInitializerList(ReadOnlyCollection<ElementInit> original)
        {
            List<ElementInit> list = null;
            for (int i = 0, n = original.Count; i < n; i++)
            {
                ElementInit init = this.VisitElementInitializer(original[i]);
                if (list != null)
                {
                    list.Add(init);
                }
                else if (init != original[i])
                {
                    list = new List<ElementInit>(n);
                    for (int j = 0; j < i; j++)
                    {
                        list.Add(original[j]);
                    }
                    list.Add(init);
                }
            }
            if (list != null)
                return list;
            return original;
        }

        protected virtual ReadOnlyCollection<Expression> VisitExpressionList(ReadOnlyCollection<Expression> original)
        {
            List<Expression> list = null;
            for (int i = 0, n = original.Count; i < n; i++)
            {
                Expression p = this.Visit(original[i]);
                if (list != null)
                {
                    list.Add(p);
                }
                else if (p != original[i])
                {
                    list = new List<Expression>(n);
                    for (int j = 0; j < i; j++)
                    {
                        list.Add(original[j]);
                    }
                    list.Add(p);
                }
            }
            if (list != null)
            {
                return list.AsReadOnly();
            }
            return original;
        }

    }

}
