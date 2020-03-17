using System;

namespace Debaser.Internals.Values
{
	internal interface IValueLookup
	{
		object GetValue(string name, Type desiredType);
	}
}