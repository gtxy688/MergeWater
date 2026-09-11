using UnityEngine;

namespace MergeWater.Aim
{
    /// <summary>一帧指针输入。Down/Held/Up 由输入源翻译，便于测试注入。</summary>
    public readonly struct PointerFrame
    {
        public readonly bool Down;
        public readonly bool Held;
        public readonly bool Up;
        public readonly bool InsideScreen;
        public readonly Vector2 ScreenPosition;

        public PointerFrame(bool down, bool held, bool up, bool insideScreen, Vector2 screenPosition)
        {
            Down = down;
            Held = held;
            Up = up;
            InsideScreen = insideScreen;
            ScreenPosition = screenPosition;
        }
    }

    /// <summary>指针输入端口，默认实现读旧版 Input（触摸由 Unity 映射为鼠标）。</summary>
    public interface IPointerSource
    {
        PointerFrame Read();
    }

    public sealed class LegacyPointerSource : IPointerSource
    {
        public PointerFrame Read()
        {
            var position = (Vector2)Input.mousePosition;
            var inside = position.x >= 0f && position.y >= 0f &&
                         position.x <= Screen.width && position.y <= Screen.height;

            return new PointerFrame(
                Input.GetMouseButtonDown(0),
                Input.GetMouseButton(0),
                Input.GetMouseButtonUp(0),
                inside,
                position);
        }
    }
}
