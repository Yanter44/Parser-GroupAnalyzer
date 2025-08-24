const API_BASE = "http://localhost:5154";

var primarybutton = document.getElementsByClassName('primaryButton')[0];
var secondaryButton = document.getElementsByClassName('secondaryButton')[0];

primarybutton.addEventListener('click', () => {
  window.location.href = "../CreateAdminPanelPage/CreateAdminPanelPage.html";
});
secondaryButton.addEventListener('click', async () => {
  try {
    const response = await fetch(`${API_BASE}/Auth/TryEnterToSessionByCookie`, {
      method: 'POST',
      credentials: 'include'
    });

    if (response.ok) {
      const result = await response.json();
      if (result === true || result.success) {
        window.location.href = "../ControlPanel/ControlPanel.html";
      } else {
        window.location.href = "../EnterToExistParserAccount/EnterToExistParserAccount.html";
      }
    } else {
      window.location.href = "../EnterToExistParserAccount/EnterToExistParserAccount.html";
    }
  } catch (err) {
    console.error('Ошибка запроса:', err);
    window.location.href = "../EnterToExistParserAccount/EnterToExistParserAccount.html";
  }
});

