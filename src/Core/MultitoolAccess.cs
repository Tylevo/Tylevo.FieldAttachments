using System;

namespace Tylevo.FieldAttachments.Core
{
    // Captures one observed tool; it grants no inventory or native-rule override by itself.
    public sealed class MultitoolAccess
    {
        public const string LeathermanTemplate = "544fb5454bdc2df8738b456a";
        public string ItemId { get; }
        public string TemplateId { get; }
        public string Location { get; }
        public object NativeItem { get; }
        public object Container { get; }
        public object Address { get; }

        internal MultitoolAccess(string itemId, string templateId, string location,
            object nativeItem, object container, object address)
        {
            if (string.IsNullOrEmpty(itemId) || templateId != LeathermanTemplate ||
                string.IsNullOrEmpty(location) || !CarriedStoragePolicy.Allows(location))
                throw new ArgumentException("An exact carried Leatherman binding is required.");
            ItemId = itemId; TemplateId = templateId; Location = location;
            NativeItem = nativeItem ?? throw new ArgumentNullException(nameof(nativeItem));
            Container = container ?? throw new ArgumentNullException(nameof(container));
            Address = address ?? throw new ArgumentNullException(nameof(address));
        }

        public bool Same(MultitoolAccess? other) => other != null &&
            ItemId == other.ItemId && TemplateId == other.TemplateId && Location == other.Location &&
            ReferenceEquals(NativeItem, other.NativeItem) && ReferenceEquals(Container, other.Container) &&
            ReferenceEquals(Address, other.Address);
    }
}
