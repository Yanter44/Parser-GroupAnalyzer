
const tg = window.Telegram.WebApp;
console.log(config.API_BASE); 
console.log(config.DefaultStartFileLocation);
document.addEventListener("DOMContentLoaded", () => {
	tg.ready();
	
     // Определяем версию и настраиваем кнопку "Назад" соответствующим образом
    if (tg && tg.initDataUnsafe) {
        const webAppVersion = parseFloat(tg.version);
        console.log('WebApp version:', webAppVersion);
        
        if (webAppVersion >= 6.1 && tg.BackButton && typeof tg.BackButton.show === 'function') {
            // Для версий 6.1+ используем стандартный BackButton
            tg.BackButton.show(); 
            tg.BackButton.onClick(goBack);
            console.log('Using standard BackButton');
        } else if (webAppVersion === 6.0) {
            // Для версии 6.0 используем события
            console.log('Using web_app_setup_back_button for version 6.0');
            
            // Показываем кнопку "Назад"
            if (tg.sendData) {
                tg.sendData(JSON.stringify({
                    method: 'web_app_setup_back_button',
                    params: { is_visible: true }
                }));
            }
            
            // Обработчик события back_button_pressed
            tg.onEvent('back_button_pressed', goBack);
        }
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
