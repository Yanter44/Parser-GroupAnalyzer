
const tg = window.Telegram.WebApp;
console.log(config.API_BASE); 
console.log(config.DefaultStartFileLocation);
document.addEventListener("DOMContentLoaded", () => {
	tg.ready();
	
     const webAppVersion = parseFloat(tg.version);
    console.log('WebApp version:', webAppVersion);
    
    // Для версий 6.1+ используем стандартный BackButton
    if (webAppVersion >= 6.1 && tg.BackButton && typeof tg.BackButton.show === 'function') {
        tg.BackButton.show(); 
        tg.BackButton.onClick(goBack);
        console.log('Using standard BackButton');
    } 
    // Для версии 6.0 используем правильный подход
    else if (webAppVersion === 6.0) {
        console.log('Using web_app_setup_back_button for version 6.0');
        
        // ПРАВИЛЬНЫЙ способ для 6.0
        if (tg && tg.isVersionAtLeast('6.0')) {
            // Используем правильный метод для показа кнопки
            try {
                // Вариант 1: Через sendData
                tg.sendData(JSON.stringify({
                    method: 'web_app_setup_back_button',
                    params: { is_visible: true }
                }));
                
                // Вариант 2: Через postEvent (более прямой)
                tg.postEvent('web_app_setup_back_button', { is_visible: true });
                
                // Вешаем обработчик
                tg.onEvent('back_button_pressed', goBack);
                
            } catch (error) {
                console.log('Error setting up back button:', error);
            }
        }
    } else {
        console.log('BackButton not supported, adding custom button');
        addCustomBackButton(); // Добавляем кастомную кнопку
    }
	
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
