
const tg = window.Telegram.WebApp;
console.log(config.API_BASE); 
console.log(config.DefaultStartFileLocation);
document.addEventListener("DOMContentLoaded", () => {
	 function initTelegramWebApp() {
        const checkInterval = setInterval(() => {
            if (window.Telegram && window.Telegram.WebApp) {
                clearInterval(checkInterval);

                const tg = window.Telegram.WebApp;

                console.log('WebApp version:', tg.version);
                console.log('пробуем пробуем');
                tg.ready();

                tg.MainButton.hide();
                if (tg.SecondaryButton) {
                    tg.SecondaryButton.hide();
                }

                setupBackButton(tg);

                try {
                    window.parent.postMessage(JSON.stringify({
                        eventType: 'web_app_setup_closing_behavior',
                        params: { need_confirmation: false }
                    }), '*');
                } catch (error) {
                    console.error('Error setting closing behavior:', error);
                }
            }
        }, 100);
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
function setupBackButton(tg) {
    if (tg.BackButton) {
        try {
            tg.BackButton.show();
            tg.BackButton.onClick(() => {
                console.log('Back button clicked');
               window.history.back();
            });
            console.log('Back button initialized');
        } catch (error) {
            console.error('Error with BackButton:', error);
            createFallbackBackButton(); 
        }
    } else {
        console.warn('BackButton API not available, using fallback');
        createFallbackBackButton();
    }
}