// Whether the browser really has the mouse locked to the game.
//
// Unity's Cursor.lockState says what the game ASKED for, and in a browser that is not the same thing:
// Esc releases the lock at the browser's level and Unity is not always told, so the game went on
// believing the mouse was locked -- the camera kept turning under a free cursor and no menu opened.
mergeInto(LibraryManager.library, {
  WebPointerIsLocked: function () {
    return document.pointerLockElement ? 1 : 0;
  }
});
