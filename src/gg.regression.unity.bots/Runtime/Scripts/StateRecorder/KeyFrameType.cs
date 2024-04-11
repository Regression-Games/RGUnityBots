namespace StateRecorder
{
    public enum KeyFrameType
    {
        FIRST_FRAME,
        SCENE,
        GAME_ELEMENT,
        GAME_ELEMENT_RENDERER_COUNT,
        UI_ELEMENT,
        UI_GAMEFACE
    }

    public static class KeyFrameTypeExtensions
    {
        public static string ToJson(this KeyFrameType _this) {
            return "\"" + _this.ToString() + "\"";
        }
    }
}
