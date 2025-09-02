
const tg = window.Telegram.WebApp;
console.log(config.API_BASE); 
console.log(config.DefaultStartFileLocation);
document.addEventListener("DOMContentLoaded", () => {
	function initTelegramWebApp() {
        if (window.Telegram && window.Telegram.WebApp) {
            const tg = window.Telegram.WebApp;
            
            console.log('WebApp version:', tg.version);
            tg.ready();

            // Скрываем основные кнопки
            tg.MainButton.hide();
            if (tg.SecondaryButton) {
                tg.SecondaryButton.hide();
            }

            // ПОПЫТКА ВЫЗВАТЬ НАТИВНУЮ КНОПКУ TELEGRAM
            try {
                // Способ 1: Через postMessage (основной для web)
                if (window.parent && window.parent !== window) {
                    window.parent.postMessage(JSON.stringify({
                        eventType: 'web_app_setup_back_button',
                        params: { is_visible: true }
                    }), '*');
                    console.log('Native back button requested via postMessage');
                }
                
                // Способ 2: Через расширенный API (если доступно)
                if (typeof window.Telegram?.WebApp?.postEvent === 'function') {
                    window.Telegram.WebApp.postEvent('web_app_setup_back_button', { 
                        is_visible: true 
                    });
                    console.log('Native back button requested via postEvent');
                }
                
                // Обработчик нативной кнопки
                tg.onEvent('back_button_pressed', function() {
                    console.log('Native back button pressed - going back');
                    if (window.history.length > 1) {
                        window.history.back();
                    } else {
                        tg.close();
                    }
                });
                
            } catch (error) {
                console.error('Error requesting native button:', error);
                // Если не получилось, покажем alert с инструкцией
                alert('Кнопка "Назад" недоступна в этой версии Telegram. Используйте жесты или системную кнопку "Назад".');
            }
            
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
    } else {
        if (window.Telegram && window.Telegram.WebApp && window.Telegram.WebApp.close) {
            window.Telegram.WebApp.close();
        }
    }
}
