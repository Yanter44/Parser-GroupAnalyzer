const tg = window.Telegram.WebApp;
var primarybutton = document.getElementsByClassName('primaryButton')[0];
var secondaryButton = document.getElementsByClassName('secondaryButton')[0];

console.log(config.API_BASE); 
console.log(config.DefaultStartFileLocation); 

document.addEventListener("DOMContentLoaded", () => {
    function initTelegramWebApp() {
        if (window.Telegram && window.Telegram.WebApp) {
            const tg = window.Telegram.WebApp;

            console.log('WebApp version:', tg.version);
            tg.ready();

            if (tg.BackButton) {          
                tg.BackButton.hide();            
            }

        } else {
            setTimeout(initTelegramWebApp, 100);
        }
    }

    initTelegramWebApp();
});

primarybutton.addEventListener('click', () => {
  window.location.href = `${config.DefaultStartFileLocation}/CreateAdminPanelPage/CreateAdminPanelPage.html`;
});
secondaryButton.addEventListener('click', async () => {
  try {
    const response = await fetch(`${config.API_BASE}/Auth/TryEnterToSessionByCookie`, {
      method: 'POST',
      credentials: 'include'
    });

    if (response.ok) {
      const result = await response.json();
      if (result === true || result.success) {
        window.location.href = `${config.DefaultStartFileLocation}/ControlPanel/ControlPanel.html`;
      } else {
        window.location.href = `${config.DefaultStartFileLocation}/EnterToExistParserAccount/EnterToExistParserAccount.html`;
      }
    } else {
      window.location.href = `${config.DefaultStartFileLocation}/EnterToExistParserAccount/EnterToExistParserAccount.html`;
    }
  } catch (err) {
    console.error('Ошибка запроса:', err);
    window.location.href = `${config.DefaultStartFileLocation}/EnterToExistParserAccount/EnterToExistParserAccount.html`;
  }
});