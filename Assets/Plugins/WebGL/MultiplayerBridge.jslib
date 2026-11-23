mergeInto(LibraryManager.library, {
  MP_SendLocalState: function (jsonPtr) {
    try {
      var json = UTF8ToString(jsonPtr);
      if (window.MyBUPGI_MP && typeof window.MyBUPGI_MP.onUnityLocalState === "function") {
        window.MyBUPGI_MP.onUnityLocalState(json);
      }
    } catch (e) {}
  },

  MP_NotifyReady: function () {
    try {
      if (window.MyBUPGI_MP && typeof window.MyBUPGI_MP.onUnityReady === "function") {
        window.MyBUPGI_MP.onUnityReady();
      }
    } catch (e) {}
  }
});
