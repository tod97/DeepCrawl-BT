#if (NET_4_6 || NET_STANDARD_2_0)

using System.Collections.Generic;

namespace Unity.Properties.Editor.Serialization
{
    public interface IPropertyTypeNodeDeserializer
    {
        List<PropertyTypeNode> Deserialize();
    }
}

#endif // (NET_4_6 || NET_STANDARD_2_0)
