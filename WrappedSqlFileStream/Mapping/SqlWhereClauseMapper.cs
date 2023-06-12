using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq.Expressions;

namespace WrappedSqlFileStream.Mapping
{
    public class SqlWhereClauseMapper<T> : SqlTypeMapper
    {
        private List<KeyValuePair<SqlParameter, object>> _parameters;
        private static SqlDbType GetSqlDbType(Type type)
        {
            if (type == typeof(int))
            {
                return SqlDbType.Int;
            }
            else if (type == typeof(byte))
            {
                return SqlDbType.SmallInt;
            }
            else if (type == typeof(short))
            {
                return SqlDbType.SmallInt;
            }
            else if (type == typeof(long))
            {
                return SqlDbType.BigInt;
            }
            else if (type == typeof(bool))
            {
                return SqlDbType.Bit;
            }
            else if (type == typeof(string))
            {
                return SqlDbType.VarChar;
            }
            else if (type == typeof(decimal))
            {
                return SqlDbType.Decimal;
            }
            else if (type == typeof(float))
            {
                return SqlDbType.Float;
            }
            else if (type == typeof(DateTime))
            {
                return SqlDbType.DateTime;
            }
            else if (type == typeof(Guid))
            {
                return SqlDbType.UniqueIdentifier;
            }
            throw new Exception("Unknown type");
        }

        public SqlWhereClauseMapper(Dictionary<string, string> mappings) : base(mappings)
        {
        }

        //
        public WhereClauseResult MapExpression<TResult>(Expression<Func<T, bool>> expression)
        {
            return Map(expression.Body);
        }

        public WhereClauseResult Map(Expression expression)
        {
            _parameters = new List<KeyValuePair<SqlParameter, object>>();
            var result = new WhereClauseResult
            {
                WhereClause = VisitNode(expression),
                Parameters = _parameters
            };
            return result;
        }

        public string VisitNode(Expression node)
        {
            if (node is MemberExpression)
            {
                var mnode = ((MemberExpression)node);
                if (mnode.Expression is ParameterExpression)
                {
                    var propertyName = ((MemberExpression)node).Member.Name;

                    if (_mappings.ContainsKey(propertyName))
                    {
                        return $"{_mappings[propertyName]}";
                    }
                    else
                    {
                        throw new Exception($"Failed to map property {propertyName} of type {typeof(T).Name}");
                    }
                }
                else
                {
                    var value = Expression.Lambda<Func<object>>(Expression.Convert(mnode, typeof(object))).Compile()();
                    var paramname = $"@p{_parameters.Count}";
                    _parameters.Add(new KeyValuePair<SqlParameter, object>(new SqlParameter(paramname, GetSqlDbType(value.GetType())), value));
                    return $"{paramname}";
                }
            }
            else if (node is ConstantExpression)
            {
                var value = ((ConstantExpression)node).Value;
                var paramname = $"@p{_parameters.Count}";
                _parameters.Add(new KeyValuePair<SqlParameter, object>(new SqlParameter(paramname, GetSqlDbType(value.GetType())), value));
                return $"{paramname}";
            }
            else if (node is BinaryExpression)
            {
                var bnode = (BinaryExpression)node;
                var left = VisitNode(bnode.Left);
                var right = VisitNode(bnode.Right);
                string operation = null;
                switch (bnode.NodeType)
                {
                    case ExpressionType.Equal:
                        operation = "=";
                        break;
                    case ExpressionType.AndAlso:
                        operation = "AND";
                        break;
                    case ExpressionType.OrElse:
                        operation = "OR";
                        break;
                    default:
                        throw new Exception("Unsupported operator");
                }
                return $"({left} {operation} {right})";
            }

            throw new Exception("Unsupported expression");
        }
    }
}