﻿// Copyright (c) Zain Al-Ahmary.  All rights reserved.
// Licensed under the MIT License, (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at https://mit-license.org/

using ReddWare.Language.Json.Reflection;
using System.Text;

namespace ReddWare.Language.Json.Conversion
{
    /// <summary>
    /// Stores json objects' properties
    /// </summary>
    class JsonBag : JsonBase
    {
        /// <summary>
        /// Ties a value to property name
        /// </summary>
        public Dictionary<string, JsonBase> Values { get; private set; }

        /// <summary>
        /// Creates an instance
        /// </summary>
        public JsonBag() : base()
        {
            JsonContainerName = "JsonBag";
            Values = new Dictionary<string, JsonBase>();
        }

        /// <summary>
        /// Returns the JsonBag as a json string
        /// </summary>
        /// <param name="appendTypeProperty">Whether or not JSON should include the $type property</param>
        /// <returns></returns>
        public override string AsJson(bool appendTypeProperty)
        {
            var result = new StringBuilder("{");
            var keyCount = 0;

            if (appendTypeProperty)
            {
                ProperlyAddType(result, ConvertTarget, Values.Keys.Count);
            }

            foreach (var item in Values.Keys)
            {
                keyCount++;

                result.Append($"\"{item}\":{Values[item].AsJson(appendTypeProperty)}");
                if (keyCount < Values.Keys.Count)
                {
                    result.Append(",");
                }
            }

            result.Append("}");
            return result.ToString();
        }

        /// <summary>
        /// Returns the object the JsonBase represents
        /// </summary>
        /// <returns></returns>
        public override object AsObject()
        {
            object result = null;
            if (ConvertTarget != null)
            {
                result = GetInstance();
                var props = TypeHelper.GetValidProperties(ConvertTarget);
                foreach (var p in props)
                {
                    p.SetValue(result, Values[p.Name].AsObject());
                }
            }

            return result;
        }

        public override string ToString()
        {
            return $"Count: {Values.Count}, Type: {ConvertTarget?.Name}";
        }

        /// <summary>
        /// Returns a structure-derived string that should be identical across any record of the same type (not intended to be unique across all types)
        /// </summary>
        /// <returns></returns>
        public override string GetKeyString()
        {
            var sb = new StringBuilder();
            foreach (var v in Values)
            {
                if (sb.Length > 0)
                {
                    sb.Append(",");
                }
                sb.Append(v.Key);
            }

            return $"[{sb}]";
        }
    }
}
