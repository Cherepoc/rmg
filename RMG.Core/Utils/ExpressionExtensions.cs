using System;
using System.Linq.Expressions;

namespace RMG.Core.Utils
{
    public static class ExpressionExtensions
    {
        public static string GetPropertyName<TSource, TProperty>(
            this Expression<Func<TSource, TProperty>> propertyLambda
        )
        {
            var body = propertyLambda.Body as MemberExpression;
            if (body == null)
            {
                var ubody = (UnaryExpression) propertyLambda.Body;
                body = ubody.Operand as MemberExpression;
            }

            return body.Member.Name;
        }
    }
}
