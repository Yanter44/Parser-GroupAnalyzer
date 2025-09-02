
const tg = window.Telegram.WebApp;
console.log(config.API_BASE); 
console.log(config.DefaultStartFileLocation);
document.addEventListener("DOMContentLoaded", () => {
	  function initTelegramWebApp() {
        if (typeof Telegram !== 'undefined' && Telegram.WebApp) {
            const tg = Telegram.WebApp;
            
            console.log('WebApp version:', tg.version);
            
            // НАСТРОЙКА КНОПКИ НАЗАД через postMessage
            try {
                // Отправляем команду для показа кнопки "Назад"
                window.parent.postMessage(JSON.stringify({
                    eventType: 'web_app_setup_back_button',
                    params: { is_visible: true }
                }), '*');
                
                console.log('Back button setup command sent');
                
                // Обработчик нажатия кнопки "Назад"
                window.addEventListener('message', function(event) {
                    try {
                        const data = JSON.parse(event.data);
                        if (data.eventType === 'back_button_pressed') {
                            console.log('Back button pressed - closing app');
                            tg.close();
                        }
                    } catch (e) {
                        // Не our message
                    }
                });
                
            } catch (error) {
                console.error('Error setting up back button:', error);
            }
            
            // Настройка поведения при закрытии (опционально)
            try {
                window.parent.postMessage(JSON.stringify({
                    eventType: 'web_app_setup_closing_behavior',
                    params: { need_confirmation: false }
                }), '*');
            } catch (error) {
                console.error('Error setting closing behavior:', error);
            }
            
            // Скрываем основные кнопки
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
