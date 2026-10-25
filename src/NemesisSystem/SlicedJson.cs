using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NemesisSystem;

public sealed class SlicedJson
{
	private const int ItemsPerStep = 8;

	private readonly JsonSerializer _ser = JsonSerializer.CreateDefault();

	private readonly StringWriter _sw = new StringWriter(new StringBuilder(65536), CultureInfo.InvariantCulture);

	private readonly JsonTextWriter _w;

	private readonly IEnumerator<bool> _steps;

	public object Root { get; }

	public bool Done { get; private set; }

	public int Steps { get; private set; }

	public string Text
	{
		get
		{
			if (!Done)
			{
				return null;
			}
			return _sw.ToString();
		}
	}

	public SlicedJson(object root)
	{
		Root = root;
		_w = new JsonTextWriter(_sw)
		{
			Formatting = _ser.Formatting
		};
		_steps = Walk(root).GetEnumerator();
	}

	public bool Step()
	{
		if (Done)
		{
			return true;
		}
		Steps++;
		if (!_steps.MoveNext())
		{
			_w.Flush();
			Done = true;
		}
		return Done;
	}

	public string Finish()
	{
		while (!Step())
		{
		}
		return Text;
	}

	private IEnumerable<bool> Walk(object root)
	{
		if (root == null)
		{
			_w.WriteNull();
			yield break;
		}
		if (root is IList source && !(root is Array) && _ser.ContractResolver.ResolveContract(root.GetType()) is JsonArrayContract c && Plain(c))
		{
			object[] array = source.Cast<object>().ToArray();
			_w.WriteStartArray();
			object[] array2 = array;
			foreach (object value in array2)
			{
				_ser.Serialize(_w, value);
				yield return true;
			}
			_w.WriteEndArray();
			yield break;
		}
		if (!(_ser.ContractResolver.ResolveContract(root.GetType()) is JsonObjectContract jsonObjectContract) || !Plain(jsonObjectContract))
		{
			_ser.Serialize(_w, root);
			yield break;
		}
		_w.WriteStartObject();
		foreach (JsonProperty property in jsonObjectContract.Properties)
		{
			if (property.Ignored || !property.Readable || (property.ShouldSerialize != null && !property.ShouldSerialize(root)) || (property.GetIsSpecified != null && !property.GetIsSpecified(root)))
			{
				continue;
			}
			object value2 = property.ValueProvider.GetValue(root);
			_w.WritePropertyName(property.PropertyName);
			if (value2 == null)
			{
				_w.WriteNull();
				yield return true;
				continue;
			}
			if (property.Converter != null && property.Converter.CanWrite)
			{
				property.Converter.WriteJson(_w, value2, _ser);
				yield return true;
				continue;
			}
			if (value2 is IList list && !(value2 is Array) && list.Count > 8 && _ser.ContractResolver.ResolveContract(value2.GetType()) is JsonArrayContract c2 && Plain(c2))
			{
				object[] array2 = list.Cast<object>().ToArray();
				_w.WriteStartArray();
				for (int i = 0; i < array2.Length; i++)
				{
					_ser.Serialize(_w, array2[i]);
					if ((i + 1) % 8 == 0)
					{
						yield return true;
					}
				}
				_w.WriteEndArray();
			}
			else
			{
				_ser.Serialize(_w, value2);
			}
			yield return true;
		}
		_w.WriteEndObject();
	}

	private static bool Plain(JsonObjectContract c)
	{
		if (c.Converter != null || c.ExtensionDataGetter != null || c.IsReference == true || c.ItemConverter != null)
		{
			return false;
		}
		if (c.OnSerializingCallbacks.Count > 0 || c.OnSerializedCallbacks.Count > 0)
		{
			return false;
		}
		foreach (JsonProperty property in c.Properties)
		{
			if (!property.Ignored && (property.NullValueHandling.HasValue || property.DefaultValueHandling.HasValue || property.ReferenceLoopHandling.HasValue || property.TypeNameHandling.HasValue || property.IsReference.HasValue || property.ItemConverter != null || property.ItemIsReference.HasValue || property.ItemTypeNameHandling.HasValue || property.ItemReferenceLoopHandling.HasValue))
			{
				return false;
			}
		}
		return true;
	}

	private static bool Plain(JsonArrayContract c)
	{
		if (c.Converter == null && c.IsReference != true && c.ItemConverter == null && c.ItemIsReference != true && !c.ItemTypeNameHandling.HasValue && c.OnSerializingCallbacks.Count == 0)
		{
			return c.OnSerializedCallbacks.Count == 0;
		}
		return false;
	}
}
