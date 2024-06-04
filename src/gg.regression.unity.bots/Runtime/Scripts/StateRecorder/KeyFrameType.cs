namespace StateRecorder
{
    public enum KeyFrameType
    {
        FIRST_FRAME,
        //No longer used as it overlapped with UI_ELEMENT/GAME_ELEMENT
        SCENE,
        GAME_ELEMENT,

        //TODO: Implement ME into bot segments
        GAME_ELEMENT_RENDERER_COUNT,
        UI_ELEMENT,

        //TODO: Implement ME for bot segments
        UI_PIXELHASH,
        TIMER
    }
}
