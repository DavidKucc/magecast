// Copies text to the browser's clipboard. Unity's own GUIUtility.systemCopyBuffer only reaches a
// clipboard inside the Unity player in a web build, so the room code would never leave the page.
mergeInto(LibraryManager.library, {
  WebClipboardCopy: function (textPtr) {
    var text = UTF8ToString(textPtr);
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(function (e) { console.warn("clipboard: " + e); });
    }
  }
});
