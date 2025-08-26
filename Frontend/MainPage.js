import { config } from './Config.js';

var primarybutton = document.getElementsByClassName('primaryButton')[0];
var secondaryButton = document.getElementsByClassName('secondaryButton')[0];

console.log(config.API_BASE); 
console.log(config.DefaultStartFileLocation); 

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

