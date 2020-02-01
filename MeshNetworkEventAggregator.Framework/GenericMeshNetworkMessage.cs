namespace MeshNetworkEventAggregator.Framework
{
    /// <summary>
    /// Generic message with user specified content
    /// </summary>
    /// <typeparam name="TContent">Content type to store</typeparam>
    public class GenericMeshNetworkMessage<TContent> : MeshNetworkMessageBase
    {
        /// <summary>
        /// Contents of the message
        /// </summary>
        public TContent Content { get; protected set; }

        /// <summary>
        /// Create a new instance of the GenericMeshMessage class.
        /// </summary>
        /// <param name="sender">Message sender (usually "this")</param>
        /// <param name="content">Contents of the message</param>
        public GenericMeshNetworkMessage(object sender, TContent content)
            : base(sender)
        {
            Content = content;
        }
    }
}