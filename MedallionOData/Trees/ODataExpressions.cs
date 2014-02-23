﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Medallion.OData.Trees
{
	internal sealed class ODataBinaryOpExpression : ODataExpression
	{
		// TODO implicit cast checks (& insertions), static factory methods
		internal ODataBinaryOpExpression(ODataExpression left, ODataBinaryOp @operator, ODataExpression right)
			: base(ODataExpressionKind.BinaryOp, @operator.IsBooleanOp() ? ODataExpressionType.Boolean : left.Type)
		{
			Throw<ArgumentException>.If(right.Type != left.Type, "right & left: must have equal types");
			this.Right = right;
			this.Operator = @operator;
			this.Left = left;
		}

		public ODataExpression Right { get; private set; }
		public ODataBinaryOp Operator { get; private set; }
		public ODataExpression Left { get; private set; }

		public override string ToString()
		{
			return new StringBuilder()
				.AppendFormat(this.NeedsParens(this.Left) ? "({0}) " : "{0} ", this.Left)
				.Append(this.Operator.ToODataString())
				.AppendFormat(this.NeedsParens(this.Right) ? " ({0})" : " {0}", this.Right)
				.ToString();
		}

		private bool NeedsParens(ODataExpression leftOrRight)
		{
			if (leftOrRight.Kind != ODataExpressionKind.BinaryOp)
			{
				return false;
			}
			var binaryOp = (ODataBinaryOpExpression)leftOrRight;
			switch (binaryOp.Operator)
			{
				case ODataBinaryOp.Or:
					return this.Operator > ODataBinaryOp.Or;

				case ODataBinaryOp.And:
				case ODataBinaryOp.Equal:
				case ODataBinaryOp.NotEqual:
				case ODataBinaryOp.GreaterThan:
				case ODataBinaryOp.GreaterThanOrEqual:
				case ODataBinaryOp.LessThan:
				case ODataBinaryOp.LessThanOrEqual:
					return this.Operator > ODataBinaryOp.And;

				case ODataBinaryOp.Add: 
				case ODataBinaryOp.Subtract:
				case ODataBinaryOp.Multiply:
				case ODataBinaryOp.Divide:
				case ODataBinaryOp.Modulo:
					return this.Operator > ODataBinaryOp.Subtract;
				default:
					throw Throw.UnexpectedCase(binaryOp);
			}
		}
	}

	internal sealed class ODataUnaryOpExpression : ODataExpression
	{
		internal ODataUnaryOpExpression(ODataExpression operand, ODataUnaryOp @operator)
			: base(ODataExpressionKind.UnaryOp, operand.Type)
		{
			this.Operand = operand;
			this.Operator = @operator;
		}

		public ODataExpression Operand { get; private set; }
		public ODataUnaryOp Operator { get; private set; }

		public override string ToString()
		{
			return string.Format("{0} {1}", this.Operator.ToODataString(), this.Operand);
		}
	}

	internal sealed class ODataCallExpression : ODataExpression
	{
		internal ODataCallExpression(ODataFunction function, IReadOnlyList<ODataExpression> arguments, ODataExpressionType returnType)
			: base(ODataExpressionKind.Call, returnType)
		{
			this.Function = function;
			this.Arguments = arguments;
		}

		public ODataFunction Function { get; private set; }
		public IReadOnlyList<ODataExpression> Arguments { get; private set; }

		public override string ToString()
		{
			return string.Format("{0}({1})", this.Function.ToODataString(), this.Arguments.ToDelimitedString(", "));
		}
	}

	internal sealed class ODataConstantExpression : ODataExpression
	{
		internal ODataConstantExpression(object value, ODataExpressionType type)
			: base(ODataExpressionKind.Constant, type)
		{
			this.Value = value;
		}

		public object Value { get; private set; }

		public override string ToString()
		{
			if (this.Value == null)
			{
				return "null";
			}
			switch (this.Type)
			{
				case ODataExpressionType.Binary:
					throw new NotImplementedException("Binary");
				case ODataExpressionType.Boolean:
					return (bool)this.Value ? "true" : "false";
				case ODataExpressionType.Byte:
					return System.Convert.ToString((byte)this.Value, toBase: 16);
				case ODataExpressionType.DateTime:
					// the datetime format & regex is very precise. This logic ensures round-tripping of dates
					var dt = (DateTime)this.Value;
					var dateTimeBuilder = new StringBuilder()
						.AppendFormat("datetime'{0:0000}-{1:00}-{2:00}T{3:00}:{4:00}", dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute);
					var seconds = (dt.TimeOfDay - new TimeSpan(hours: dt.Hour, minutes: dt.Minute, seconds: 0)).TotalSeconds;
					if (seconds > 0)
					{
						dateTimeBuilder.AppendFormat(":{0:00}", dt.Second);
						if (seconds > dt.Second)
						{
							dateTimeBuilder.AppendFormat("{0:.0000000}", seconds - dt.Second);
						}
					}
					return dateTimeBuilder.Append('\'').ToString();
				case ODataExpressionType.Decimal:
					return this.Value + "M";
				case ODataExpressionType.Double:
					return this.Value.ToString();
				case ODataExpressionType.Guid:
					return string.Format("guid'{0}'", this.Value);
				case ODataExpressionType.Int16:
				case ODataExpressionType.Int32:
					return this.Value.ToString();
				case ODataExpressionType.Int64:
					return this.Value + "L";
				case ODataExpressionType.Single:
					return ((float)this.Value).ToString("0.0") + "f";
				case ODataExpressionType.String:
					// escaping as in http://stackoverflow.com/questions/3979367/how-to-escape-a-single-quote-to-be-used-in-an-odata-query
					return string.Format("'{0}'", ((string)this.Value).Replace("'", "''"));
				case ODataExpressionType.Type:
					var clrType = (Type)this.Value;
					var oDataType = clrType.ToODataExpressionType();
					if (oDataType.IsPrimitive())
					{
						return oDataType.ToODataString();
					}
					return string.Format("'{0}'", clrType);
				default:
					throw Throw.UnexpectedCase(this.Type);
			}
		}
	}

	internal sealed class ODataMemberAccessExpression : ODataExpression
	{
		public ODataMemberAccessExpression(ODataMemberAccessExpression expression, PropertyInfo member)
			: base(ODataExpressionKind.MemberAccess, member.PropertyType.ToODataExpressionType())
		{
			this.Expression = expression;
			this.Member = member;
		}

		public ODataMemberAccessExpression Expression { get; private set; }
		public PropertyInfo Member { get; private set; }

		public override string ToString()
		{
			return this.Expression != null
				? string.Format("{0}/{1}", this.Expression, this.Member.Name)
				: this.Member.Name;
		}
	}

	internal sealed class ODataConvertExpression : ODataExpression
	{
		internal ODataConvertExpression(ODataExpression expression, ODataExpressionType type)
			: base(ODataExpressionKind.Convert, type)
		{
			this.Expression = expression;
		}

		public ODataExpression Expression { get; private set; }

		public override string ToString()
		{			
			return this.Expression.Type.IsImplicityCastableTo(this.Type)
				? this.Expression.ToString()
				: string.Format("cast({0}, {1})", this.Expression, this.Type.ToODataString());
		}
	}

	internal sealed class ODataSortKeyExpression : ODataExpression
	{
		internal ODataSortKeyExpression(ODataExpression expression, ODataSortDirection direction)
			: base(ODataExpressionKind.SortKey, expression.Type)
		{
			this.Expression = expression;
			this.Direction = direction;
		}

		public ODataExpression Expression { get; private set; }
		public ODataSortDirection Direction { get; private set; }

		public override string ToString()
		{
			return this.Expression + (this.Direction == ODataSortDirection.Ascending ? string.Empty : " " + this.Direction.ToODataString());
		}
	}

	internal sealed class ODataSelectColumnExpression : ODataExpression
	{
		internal ODataSelectColumnExpression(ODataMemberAccessExpression expression, bool allColumns)
			: base(ODataExpressionKind.SelectColumn, expression != null ? expression.Type : ODataExpressionType.Complex)
		{
			this.Expression = expression;
			this.AllColumns = allColumns;
		}

		public ODataMemberAccessExpression Expression { get; private set; }
		public bool AllColumns { get; private set; }

		public override string ToString()
		{
			var sb = new StringBuilder().Append(this.Expression);
			if (this.AllColumns)
			{
				if (this.Expression != null)
				{
					sb.Append('/');
				}
				sb.Append('*');
			}
			return sb.ToString();
		}
	}

	internal class ODataQueryExpression : ODataExpression
	{
		// TODO expand
		// TODO select
		internal ODataQueryExpression(
			ODataExpression filter,
			IReadOnlyList<ODataSortKeyExpression> orderBy,
			int? top,
			int skip,
			string format,
			ODataInlineCountOption inlineCount,
			IReadOnlyList<ODataSelectColumnExpression> select)
			: base(ODataExpressionKind.Query, ODataExpressionType.Complex)
		{
			this.Filter = filter;
			this.OrderBy = orderBy;
			this.Top = top;
			this.Skip = skip;
			this.Format = format;
			this.InlineCount = inlineCount;
			this.Select = select;
		}

		public ODataExpression Filter { get; private set; }
		public IReadOnlyList<ODataSortKeyExpression> OrderBy { get; private set; }
		public int? Top { get; private set; }
		public int Skip { get; private set; }
		public string Format { get; private set; }
		public ODataInlineCountOption InlineCount { get; private set; }
		public IReadOnlyList<ODataSelectColumnExpression> Select { get; private set; } 

		public NameValueCollection ToNameValueCollection()
		{
			var result = new NameValueCollection();
			if (this.Filter != null)
			{
				result.Add("$filter", this.Filter.ToString());
			}
			if (this.OrderBy.Count > 0)
			{
				result.Add("$orderby", this.OrderBy.ToDelimitedString());
			}
			if (this.Top.HasValue)
			{
				result.Add("$top", this.Top.ToString());
			}
			if (this.Skip != 0)
			{
				result.Add("$skip", this.Skip.ToString());
			}
			if (this.Format != null)
			{
				result.Add("$format", this.Format);
			}
			if (this.InlineCount != ODataInlineCountOption.None)
			{
				result.Add("$inlinecount", this.InlineCount.ToODataString());
			}
			if (this.Select.Count > 0)
			{
				result.Add("$select", this.Select.ToDelimitedString());
			}

			return result;
		}

		public override string ToString()
		{
			var builder = new StringBuilder("?");
			var values = this.ToNameValueCollection();
			foreach (string key in values)
			{
				AppendParam(builder, key, values[key]);
			}

			return builder.ToString();
		}

		public ODataQueryExpression Update(ODataExpression filter = null, IEnumerable<ODataSortKeyExpression> orderBy = null, int? top = -1, int? skip = null, string format = null, ODataInlineCountOption? inlineCount = null, IEnumerable<ODataSelectColumnExpression> select = null)
		{
			return Query(
				filter: filter ?? this.Filter,
				orderBy: orderBy.NullSafe(ob => ob.ToArray(), ifNullReturn: this.OrderBy),
				top: top == -1 ? this.Top : top,
				skip: skip ?? this.Skip,
				format: format ?? this.Format,
				inlineCount: inlineCount ?? this.InlineCount,
				select: select.NullSafe(sc => sc.ToArray(), ifNullReturn: this.Select)
			);
		}

		private static void AppendParam(StringBuilder builder, string paramName, string value)
		{
			if (builder[builder.Length - 1] != '?')
			{
				builder.Append('&');
			}
			builder.Append(paramName)
				.Append('=')
				.Append(HttpUtility.UrlEncode(value));
		}
	}
}