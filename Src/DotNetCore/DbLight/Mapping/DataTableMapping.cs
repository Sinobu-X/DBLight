﻿using System;
using System.Collections.Generic;
using System.Data;
using DbLight.Common;

namespace DbLight.Mapping
{
    public class DataTableMapping<T1, T2> where T1 : new()
    {
        private readonly DbModelInfo _model;
        private readonly List<(string TableName, string ColumnName)> _columns;
        private readonly Func<T1, T2> _converter;
        private readonly List<T2> _results;

        public DataTableMapping(DataColumnCollection columns, Func<T1, T2> converter){
            _model = DbModelHelper.GetModelInfo(typeof(T1));

            if (_model.Kind == DbModelKind.Object || _model.Kind == DbModelKind.Tuple){
                //ok
            }
            else{
                throw new Exception("The receiver data type muse be an object or tuple.");
            }

            _columns = new List<(string TableName, string ColumnName)>();
            for (var i = 0; i < columns.Count; i++){
                var column = columns[i];

                string tableName;
                string columnName;
                var pos = column.ColumnName.IndexOf(".", StringComparison.Ordinal);
                if (pos >= 0){
                    tableName = column.ColumnName.Substring(0, pos);
                    columnName = column.ColumnName.Substring(pos + 1);
                }
                else{
                    tableName = "";
                    columnName = column.ColumnName;
                }

                _columns.Add((tableName, columnName));
            }

            _converter = converter;
            _results = new List<T2>();
        }

        public void AddRow(object[] values){
            if (_model.Kind == DbModelKind.Object){
                AddRowForObject(values);
            }
            else if (_model.Kind == DbModelKind.Tuple){
                AddRowForTuple(values);
            }
        }

        private void AddRowForObject(object[] values){
            var item = new T1();

            for (var i = 0; i < values.Length; i++){
                if (i >= _columns.Count){
                    continue;
                }

                var value = values[i];
                if (value is DBNull){
                    continue;
                }

                var column = _columns[i];
                var objectItemIndex = _model.Members.FindIndex(x =>
                    x.ColumnName.Equals(column.TableName, StringComparison.OrdinalIgnoreCase));
                if (objectItemIndex < 0){
                    //is main object
                    var p = _model.Members.Find(x =>
                        x.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase));
                    if (p == null){
                        continue;
                    }

                    if (p.NotMapped){
                        continue;
                    }

                    if (p.Model.Kind != DbModelKind.Value){
                        continue;
                    }

                    p.PropertyInfo.SetValue(item, value);
                }
                else{
                    var objectItemInfo = _model.Members[objectItemIndex];
                    if (objectItemInfo.Model.Kind != DbModelKind.Object){
                        continue;
                    }

                    var p = objectItemInfo.Model.Members.Find(x =>
                        x.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase));
                    if (p == null){
                        continue;
                    }

                    if (p.NotMapped){
                        continue;
                    }

                    if (p.Model.Kind != DbModelKind.Value){
                        continue;
                    }

                    if (objectItemInfo.MemberType != DbMemberType.Property){
                        continue;
                    }

                    var objectItemValue = objectItemInfo.PropertyInfo.GetValue(item);
                    if (objectItemValue == null){
                        continue;
                    }

                    p.PropertyInfo.SetValue(objectItemValue, value);
                }
            }

            _results.Add(_converter(item));
        }


        private void AddRowForTuple(object[] values){
            var tupleObj = Activator.CreateInstance(_model.Type);
            var tupleItems = new object[_model.Members.Count];
            for (var i = 0; i < _model.Members.Count; i++){
                var member = _model.Members[i];
                var item = Activator.CreateInstance(member.Model.Type);
                tupleItems[i] = item;
                member.FieldInfo.SetValue(tupleObj, item);
            }

            for (var i = 0; i < values.Length; i++){
                if (i >= _columns.Count){
                    continue;
                }

                var value = values[i];
                if (value is DBNull){
                    continue;
                }

                var column = _columns[i];
                var tupleItemIndex = _model.Members.FindIndex(x =>
                    x.ColumnName.Equals(column.TableName, StringComparison.OrdinalIgnoreCase));
                if (tupleItemIndex < 0){
                    continue;
                }

                var tupleItemInfo = _model.Members[tupleItemIndex];
                if (tupleItemInfo.Model.Kind == DbModelKind.Object){
                    var p = tupleItemInfo.Model.Members.Find(x =>
                        x.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase));
                    if (p == null){
                        continue;
                    }

                    if (p.NotMapped){
                        continue;
                    }

                    if (p.Model.Kind != DbModelKind.Value){
                        continue;
                    }

                    p.PropertyInfo.SetValue(tupleItems[tupleItemIndex], value);
                }
                else if (tupleItemInfo.Model.Kind == DbModelKind.Tuple){
                    var p = tupleItemInfo.Model.Members.Find(x =>
                        x.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase));
                    if (p == null){
                        continue;
                    }

                    if (p.Model.Kind != DbModelKind.Value){
                        continue;
                    }

                    p.FieldInfo.SetValue(tupleItems[tupleItemIndex], value);
                }
                else if (tupleItemInfo.Model.Kind == DbModelKind.Value){
                    tupleItemInfo.FieldInfo.SetValue(tupleObj, value);
                }
            }

            for (var i = 0; i < _model.Members.Count; i++){
                var tupleItemInfo = _model.Members[i];
                if (tupleItemInfo.Model.Kind == DbModelKind.Tuple){
                    tupleItemInfo.FieldInfo.SetValue(tupleObj, tupleItems[i]);
                }
            }

            _results.Add(_converter((T1) tupleObj));
        }

        public List<T2> ToList(){
            return _results;
        }
    }
}