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
        // No longer used, transform-ui, transform-worldspace, entity all map to GAME_ELEMENT now
        UI_ELEMENT,
        UI_PIXELHASH,
        TIMER
    }
}
