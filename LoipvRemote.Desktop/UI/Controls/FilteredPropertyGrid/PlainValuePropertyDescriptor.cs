using System;
using System.ComponentModel;

namespace LoipvRemote.UI.Controls.FilteredPropertyGrid
{
    internal sealed class PlainValuePropertyDescriptor(PropertyDescriptor inner) : PropertyDescriptor(inner)
    {
        private readonly PropertyDescriptor _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public override Type ComponentType => _inner.ComponentType;
        public override bool IsReadOnly => _inner.IsReadOnly;
        public override Type PropertyType => _inner.PropertyType;
        public override TypeConverter Converter => _inner.Converter;
        public override object? GetEditor(Type editorBaseType) => _inner.GetEditor(editorBaseType);
        public override bool SupportsChangeEvents => _inner.SupportsChangeEvents;

        public override bool CanResetValue(object component) => _inner.CanResetValue(component);
        public override object? GetValue(object component) => _inner.GetValue(component);
        public override void ResetValue(object component) => _inner.ResetValue(component);
        public override void SetValue(object component, object? value) => _inner.SetValue(component, value);

        // PropertyGrid renders serializable values with a bold font. The
        // configuration grid deliberately uses a single regular weight.
        public override bool ShouldSerializeValue(object component) => false;

        public override void AddValueChanged(object component, EventHandler handler) => _inner.AddValueChanged(component, handler);
        public override void RemoveValueChanged(object component, EventHandler handler) => _inner.RemoveValueChanged(component, handler);
    }
}
