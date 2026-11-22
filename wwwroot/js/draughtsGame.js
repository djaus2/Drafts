// DraughtsGame.js - JavaScript functions for DraughtsGame.razor

window.DraughtsChat = window.DraughtsChat || {};
window.DraughtsChat.scrollToBottom = function (el) {
  try {
    if (!el) return;
    el.scrollTop = el.scrollHeight;
  } catch (e) {
  }
};

window.DraughtsChat.wireEnterToSend = function (el, dotNetRef) {
  try {
    if (!el || !dotNetRef) return;
    if (el.__DraughtsEnterWired) return;
    el.__DraughtsEnterWired = true;
    el.addEventListener('keydown', function (ev) {
      try {
        if (ev.key === 'Enter') {
          ev.preventDefault();
          dotNetRef.invokeMethodAsync('OnChatEnterFromJs');
        }
      } catch (e) {
      }
    });
  } catch (e) {
  }
};

// Voice (speech recognition / TTS) lives in the GameVoice package: _content/GameVoice/gameVoice.js (window.GameVoice).
