using Vertex.Utils;

namespace Vertex.Interfaces
{
    internal interface IAudioOutputStateHandler
    {
        void OnStateChanged(AudioStateEventArgs args);
    }
}