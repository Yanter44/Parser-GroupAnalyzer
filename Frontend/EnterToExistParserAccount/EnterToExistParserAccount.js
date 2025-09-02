
const tg = window.Telegram.WebApp;
console.log(config.API_BASE); 
console.log(config.DefaultStartFileLocation);
document.addEventListener("DOMContentLoaded", () => {
	function initTelegramWebApp() {
        if (typeof Telegram !== 'undefined' && Telegram.WebApp) {
        const tg = Telegram.WebApp;
        
        console.log('WebApp version:', tg.version);
        
        try {
            tg.postEvent('web_app_setup_back_button', {is_visible: true});
            console.log('Back button SHOW command sent');
            
            // Обработчик нажатия кнопки назад
            tg.onEvent('back_button_pressed', function() {
                console.log('Back button pressed - closing app');
                tg.close();
            });
            
        } catch (error) {
            console.error('Error setting up back button:', error);
        }
        
        // Скрываем основные кнопки (это нормально)
        tg.MainButton.hide();
        if (tg.SecondaryButton) {
            tg.SecondaryButton.hide();
        }
        
        // Инициализация завершена
        tg.ready();
        
      } else {
        setTimeout(initTelegramWebApp, 100);
      }
    }

    
    // Запускаем инициализацию
    initTelegramWebApp();
	
    document.addEventListener('click', (e) => {
        if (!e.target.closest('.LoginInput') && !e.target.closest('.LoginButton')) {
            if (document.activeElement && document.activeElement.blur) {
                document.activeElement.blur();
            }
        }
    });
    const LoginForm = document.querySelector(".LoginForm");

    LoginForm?.addEventListener("submit", async e => {
        e.preventDefault();

        const body = {
            ParserId: document.getElementById("KeySession").value,
            ParserPassword: document.getElementById("ParserPassword").value,
        };
        if (!isValidGuid(body.ParserId)) {
              alert("Некорректный ключ сессии");
              return;
        }
        try {
            const res = await fetch(`${config.API_BASE}/Auth/EnterToSessionByKeyAndPassword`, {
                method: "POST",
                credentials: "include",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(body)
            });

            if (res.ok) {
                if (tg && tg.BackButton) {
                    tg.BackButton.hide();
                }
                location.href = `${config.DefaultStartFileLocation}/ControlPanel/ControlPanel.html`;
            } else {
                const errorMessage = await res.text();
                
                if(errorMessage.includes("Неверный ключ сессии или пароль")){
                    alert("Неверный ключ сессии или пароль");
                }           
            }

        } catch (err) {
            console.error(err);
            alert("Ошибка запроса: " + err.message);
        }
    });
});

function isValidGuid(guid) {
    const regex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    return regex.test(guid);
}
function goBack() {
    if (window.history.length > 1) {
        window.history.back(); 
    } else if (tg && tg.close) {
        tg.close(); 
    }
}
