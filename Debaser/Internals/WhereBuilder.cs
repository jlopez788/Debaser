using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Debaser.Internals.Query;

namespace Debaser.Internals
{
	internal static class WhereBuilder
	{
		public static WherePart ToSql<T>(Expression<Func<T, bool>> expression)
		{
			var i = 1;
			return Recurse(ref i, expression.Body, isUnary: true);
		}

		private static WherePart Concat(string @operator, WherePart operand)
		{
			return new WherePart {
				Parameters = operand.Parameters,
				Sql = $"({@operator} {operand.Sql})"
			};
		}

		private static WherePart Concat(WherePart left, string @operator, WherePart right)
		{
			return new WherePart() {
				Parameters = left.Parameters.Union(right.Parameters).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
				Sql = $"({left.Sql} {@operator} {right.Sql})"
			};
		}

		private static object GetValue(Expression member)
		{
			// source: http://stackoverflow.com/a/2616980/291955
			var objectMember = Expression.Convert(member, typeof(object));
			var getterLambda = Expression.Lambda<Func<object>>(objectMember);
			var getter = getterLambda.Compile();
			return getter();
		}

		private static WherePart IsCollection(ref int countStart, IEnumerable values)
		{
			var parameters = new Dictionary<string, object>();
			var sql = new StringBuilder("(");
			foreach (var value in values)
			{
				parameters.Add(countStart.ToString(), value);
				sql.Append($"@{countStart},");
				countStart++;
			}
			if (sql.Length == 1)
			{
				sql.Append("null,");
			}
			sql[^1] = ')';
			return new WherePart() {
				Parameters = parameters,
				Sql = sql.ToString()
			};
		}

		private static WherePart IsParameter(int count, object value)
		{
			return new WherePart() {
				Parameters = { { count.ToString(), value } },
				Sql = $"@{count}"
			};
		}

		private static WherePart IsSql(string sql)
		{
			return new WherePart() {
				Parameters = new Dictionary<string, object>(),
				Sql = sql
			};
		}

		private static string NodeTypeToString(ExpressionType nodeType)
		{
			switch (nodeType)
			{
				case ExpressionType.Add:
					return "+";

				case ExpressionType.And:
					return "&";

				case ExpressionType.AndAlso:
					return "AND";

				case ExpressionType.Divide:
					return "/";

				case ExpressionType.Equal:
					return "=";

				case ExpressionType.ExclusiveOr:
					return "^";

				case ExpressionType.GreaterThan:
					return ">";

				case ExpressionType.GreaterThanOrEqual:
					return ">=";

				case ExpressionType.LessThan:
					return "<";

				case ExpressionType.LessThanOrEqual:
					return "<=";

				case ExpressionType.Modulo:
					return "%";

				case ExpressionType.Multiply:
					return "*";

				case ExpressionType.Negate:
					return "-";

				case ExpressionType.Not:
					return "NOT";

				case ExpressionType.NotEqual:
					return "<>";

				case ExpressionType.Or:
					return "|";

				case ExpressionType.OrElse:
					return "OR";

				case ExpressionType.Subtract:
					return "-";
			}
			throw new InvalidOperationException($"Unsupported node type: {nodeType}");
		}

		private static WherePart Recurse(ref int i, Expression expression, bool isUnary = false, string prefix = null, string postfix = null)
		{
			if (expression is UnaryExpression unary)
			{
				return Concat(NodeTypeToString(unary.NodeType), Recurse(ref i, unary.Operand, true));
			}
			if (expression is BinaryExpression body)
			{
				return Concat(Recurse(ref i, body.Left), NodeTypeToString(body.NodeType), Recurse(ref i, body.Right));
			}
			if (expression is ConstantExpression constant)
			{
				var value = constant.Value;
				if (value is int)
				{
					return IsSql(value.ToString());
				}
				if (value is string s)
				{
					value = prefix + s + postfix;
				}
				if (value is bool && isUnary)
				{
					return Concat(IsParameter(i++, value), "=", IsSql("1"));
				}
				return IsParameter(i++, value);
			}
			if (expression is MemberExpression member)
			{
				if (member.Member is PropertyInfo property)
				{
					if (isUnary && member.Type == typeof(bool))
					{
						return Concat(Recurse(ref i, expression), "=", IsParameter(i++, true));
					}
					return IsSql("[" + property.Name + "]");
				}
				if (member.Member is FieldInfo)
				{
					var value = GetValue(member);
					if (value is string s)
					{
						value = prefix + s + postfix;
					}
					return IsParameter(i++, value);
				}
				throw new InvalidOperationException($"Expression does not refer to a property or field: {expression}");
			}
			if (expression is MethodCallExpression methodCall)
			{
				// LIKE queries:
				if (methodCall.Method == typeof(string).GetMethod("Contains", new[] { typeof(string) }))
				{
					return Concat(Recurse(ref i, methodCall.Object), "LIKE", Recurse(ref i, methodCall.Arguments[0], prefix: "%", postfix: "%"));
				}
				if (methodCall.Method == typeof(string).GetMethod("StartsWith", new[] { typeof(string) }))
				{
					return Concat(Recurse(ref i, methodCall.Object), "LIKE", Recurse(ref i, methodCall.Arguments[0], postfix: "%"));
				}
				if (methodCall.Method == typeof(string).GetMethod("EndsWith", new[] { typeof(string) }))
				{
					return Concat(Recurse(ref i, methodCall.Object), "LIKE", Recurse(ref i, methodCall.Arguments[0], prefix: "%"));
				}
				// IN queries:
				if (methodCall.Method.Name == "Contains")
				{
					Expression collection;
					Expression property;
					if (methodCall.Method.IsDefined(typeof(ExtensionAttribute)) && methodCall.Arguments.Count == 2)
					{
						collection = methodCall.Arguments[0];
						property = methodCall.Arguments[1];
					}
					else if (!methodCall.Method.IsDefined(typeof(ExtensionAttribute)) && methodCall.Arguments.Count == 1)
					{
						collection = methodCall.Object;
						property = methodCall.Arguments[0];
					}
					else
					{
						throw new InvalidOperationException("Unsupported method call: " + methodCall.Method.Name);
					}
					var values = (IEnumerable)GetValue(collection);
					return Concat(Recurse(ref i, property), "IN", IsCollection(ref i, values));
				}
				throw new InvalidOperationException("Unsupported method call: " + methodCall.Method.Name);
			}
			throw new InvalidOperationException("Unsupported expression: " + expression.GetType().Name);
		}

		public static List<Parameter> GetParameters(this WherePart whereClause)
		{
			return whereClause?.Parameters
					.Select(kp => new Parameter($"@{kp.Key}", kp.Value))
					.ToList() ?? new List<Parameter>(0);
		}

		internal class WherePart
		{
			public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
			public string Sql { get; set; }
		}
	}
}