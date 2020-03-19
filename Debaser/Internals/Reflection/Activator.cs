﻿using Debaser.Internals.Values;
using FastMember;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Debaser.Internals.Reflection
{
	internal class Activator
	{
		private readonly Func<IValueLookup, object> _creationFunction;

		public Activator(Type type, IEnumerable<string> includedProperties)
		{
			var propertyNames = new HashSet<string>(includedProperties);

			_creationFunction = HasDefaultConstructor(type)
				? GetPropertyCreator(type, propertyNames)
				: GetConstructorCreator(type, propertyNames);
		}

		public object CreateInstance(IValueLookup valueLookup)
		{
			return _creationFunction(valueLookup);
		}

		private static Func<IValueLookup, object> GetPropertyCreator(Type type, HashSet<string> includedProperties)
		{
			var properties = type.GetProperties()
				.Where(p => p.SetMethod != null)
				.Where(p => includedProperties.Contains(p.Name))
				.ToArray();

			var accessor = TypeAccessor.Create(type);

			return lookup => {
				var instance = System.Activator.CreateInstance(type);

				foreach (var property in properties)
				{
					var propertyName = property.Name;
					var value = lookup.GetValue(propertyName, property.PropertyType);
					try
					{
						accessor[instance, propertyName] = value;
					}
					catch (Exception exception)
					{
						throw new InvalidOperationException($"Could not set value of property {propertyName} to {value}", exception);
					}
				}

				return instance;
			};
		}

		private static Func<IValueLookup, object> GetConstructorCreator(Type type, HashSet<string> includedProperties)
		{
			var parameters = type.GetConstructors().Single()
				.GetParameters()
				.ToArray();

			return lookup => {
				var parameterValues = parameters
					.Select(parameter => {
						var titleCase = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(parameter.Name);

						if (includedProperties.Contains(titleCase))
						{
							return lookup.GetValue(parameter.Name, parameter.ParameterType);
						}

						return null;
					})
					.ToArray();

				try
				{
					var instance = System.Activator.CreateInstance(type, parameterValues);

					return instance;
				}
				catch (Exception exception)
				{
					throw new InvalidOperationException($"Could not create instance of {type} with ctor arguments {string.Join(", ", parameterValues)}", exception);
				}
			};
		}

		private static bool HasDefaultConstructor(Type type)
		{
			var constructors = type.GetConstructors();

			if (constructors.Length > 1)
			{
				throw new ArgumentException($"Cannot use {type} in the activator because it has more than one constructor. Please supply either a) a constructor with named parameters matching the type's properties, or b) a default constructor and properties with setters");
			}

			var ctor = constructors.Single();

			return ctor.GetParameters().Length == 0;
		}
	}
}