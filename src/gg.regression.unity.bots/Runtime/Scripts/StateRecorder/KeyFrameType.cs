namespace RegressionGames.StateRecorder
{
    public enum KeyFrameType
    {
        FIRST_FRAME,
        //No longer used as it overlapped with UI_ELEMENT/GAME_ELEMENT
        SCENE,
        GAME_ELEMENT,
        //No longer used as it overlapped with GAME_ELEMENT when we went to bot segments and removed roll up of objects
        GAME_ELEMENT_RENDERER_COUNT,
        UI_ELEMENT,
        //TODO: Implement me for bot segments
        UI_PIXELHASH,
        TIMER
    }
}
