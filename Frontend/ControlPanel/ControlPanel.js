//Константы и глобальные переменные
let connection;
let tickIntervalId = null;
let currentModal = null;
let tagifyInstance = null;

console.log(config.API_BASE); 
console.log(config.DefaultStartFileLocation);

const savedTags = {
    keywords: [],
    groups: []
};

const ErrorCodes = {
    NEED_VERIFICATION_CODE: "NEED_VERIFICATION_CODE",
    NEED_TWO_FACTOR_PASSWORD: "NEED_TWO_FACTOR_PASSWORD",
    INVALID_VERIFICATION_CODE: "INVALID_VERIFICATION_CODE",
    INVALID_TWO_FACTOR_PASSWORD: "INVALID_TWO_FACTOR_PASSWORD",
    SESSION_EXPIRED: "SESSION_EXPIRED",
    PHONE_ALREADY_IN_USE: "PHONE_ALREADY_IN_USE"
};

//DomElements
const AddGroupsButton = document.getElementById('AddGroupsButton');
const AddKeywordsButton = document.getElementById('AddKeywordsButton');
const closePanelBtn = document.getElementById('closePanelBtn');
const overlay = document.getElementById('overlay');
const parserTogglebutton = document.getElementById('parserToggleButton');
const icon = parserTogglebutton.querySelector('img');
const text = parserTogglebutton.querySelector('.ParserButtonText');

//Подписки на события
document.addEventListener("DOMContentLoaded", InitializePage);
closePanelBtn.addEventListener('click', closeSidePanel);
overlay.addEventListener('click', closeSidePanel);
parserTogglebutton.addEventListener('click', ValidateInputsAndTryStartParsing);

async function InitializePage() {
    history.pushState(null, null, location.href);

    window.addEventListener('popstate', function (event) {
        location.replace(`${config.DefaultStartFileLocation}/index.html`);
    });

    try {
        console.log("пробуем отправить запрос на getparserState");
        const response = await fetch(`${config.API_BASE}/ParserConfig/GetParserState`, {
            method: 'GET',
            credentials: 'include'
        });

        const result = await response.json();
        console.log(result);

          if (result.errorCode === ErrorCodes.NEED_VERIFICATION_CODE) {
            location.href = `${config.DefaultStartFileLocation}/VerifyAccountPage/VerifyAccountPage.html`
            return;
        }
        const parserLogs = result.parserLogs || [];

        if (parserLogs.length > 0) {
            const noMessagesText = document.querySelector('.DataListDoNotHaveAnyMessagesText');
            if (noMessagesText) {
                noMessagesText.remove();
            }

            parserLogs.forEach(element => {
                const container = document.querySelector(".DataList");
                const item = document.createElement("li");
                item.className = "UserParserDataItem";

                const sageProfileImageUrl = escapeUrl(element.profileImageUrl) || 'https://via.placeholder.com/100';
                const safeMessageText = escapeHtml(element.messageText);
                const safeFirstName = escapeHtml(element.firstName);
                const safeMessageTime = escapeHtml(element.messageTime);
                const localTime = new Date(safeMessageTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
                console.log(localTime);

                const img = document.createElement('img');
                img.src = sageProfileImageUrl;
                img.alt = 'User Image';
                img.className = 'ListItemImage';
                
                const nicknameDiv = document.createElement('div');
                nicknameDiv.className = 'NicknameHandler';
                nicknameDiv.textContent = safeFirstName;

                const messageTextDiv = document.createElement('div');
                messageTextDiv.className = 'MessageText';
                messageTextDiv.textContent = element.messageText;

                const messageTimeDiv = document.createElement('div');
                messageTimeDiv.className = 'MessageTime';
                messageTimeDiv.textContent = localTime;

                const spamButton = document.createElement('button');
                spamButton.className = 'ThisIsSpamButton';
                spamButton.dataset.message = safeMessageText;
                spamButton.textContent = 'Это спам';

                const telegramButton = document.createElement('button');
                telegramButton.className = 'GoToTelegramButton';
                telegramButton.dataset.username = element.username;
                telegramButton.textContent = 'Чат';

                // Собираем элементы
                item.appendChild(img);
                item.appendChild(nicknameDiv);
                item.appendChild(messageTextDiv);
                item.appendChild(messageTimeDiv);
                item.appendChild(spamButton);
                item.appendChild(telegramButton);
 
                container.prepend(item);

                // Обработчик для перехода в Telegram
                telegramButton.addEventListener('click', () => {
                    const username = telegramButton.dataset.username;
                    if (username) {
                        window.open(`https://t.me/${username}`, '_blank');
                    } else {
                        alert('Telegram username не найден');
                    }
                });
            });
        }

        // Обработка данных для интерфейса
        savedTags.keywords = Array.isArray(result.parserDataResponceDto.parserkeywords) ? result.parserDataResponceDto.parserkeywords : [];
        savedTags.groups = Array.isArray(result.parserDataResponceDto.targetGroups) ? result.parserDataResponceDto.targetGroups : [];

        if (result && result.parserDataResponceDto) {
            const { profileImageUrl, profileNickName, isParsingStarted, parserId, parserPassword, userGroupsList, targetGroups, remainingParsingTimeHoursMinutes, totalParsingTime } = result.parserDataResponceDto;
            window.userGroupsList = userGroupsList ?? [];
            window.targetGroups = targetGroups ?? [];

            const userImages = document.querySelectorAll(".UserImage");
            userImages.forEach(img => {
                img.src = profileImageUrl;
            });

            const telegramnicknameelement = document.querySelector(".ClientTelegramUserName");
            telegramnicknameelement.textContent = `@${profileNickName}`;

            const parserIdElement = document.querySelector(".ParserIdValueText");
            const parserPasswordElement = document.querySelector(".PasswordValueText");
            const totalparsingTimeElement = document.getElementsByClassName("TotalParsingTimeValueText")[0];

            if (parserIdElement) {
                parserIdElement.textContent = parserId;
            }
            if (parserPasswordElement) {
                parserPasswordElement.textContent = parserPassword;
            }
            if (totalparsingTimeElement) {
                console.log("начинаем проверку totalParsingTime: " + totalParsingTime);

                if (totalParsingTime === "") {
                    totalparsingTimeElement.textContent = "0м";
                } else {
                    totalparsingTimeElement.textContent = totalParsingTime;
                }
            }


            if (isParsingStarted) {
                setInputsEnabled(false);

                if (remainingParsingTimeHoursMinutes && remainingParsingTimeHoursMinutes !== "00:00:00") {
                    let timeElem = document.querySelector(".RemainingTimeToStopParser");

                    if (!timeElem) {
                        timeElem = document.createElement("div");
                        timeElem.className = "RemainingTimeToStopParser";

                        const btn = document.querySelector(".StartParserButton, .StopParserButton");
                        if (btn && btn.parentNode) {
                            btn.parentNode.insertBefore(timeElem, btn.nextSibling);
                        }
                    }
                    timeElem.textContent = remainingParsingTimeHoursMinutes;

                } else {
                    const timeElem = document.querySelector(".RemainingTimeToStopParser");
                    if (timeElem) timeElem.remove();
                }

                startTickTimer(remainingParsingTimeHoursMinutes);

            } else {
                setInputsEnabled(true);
                const timeElem = document.querySelector(".RemainingTimeToStopParser");
                if (timeElem) timeElem.remove();
            }

            updateParserButton(isParsingStarted ? 'stop' : 'start');

            if (isParsingStarted) {
                setInputsEnabled(false);
                startSignalR();
            }
        }
    } catch (error) {
        console.error('Ошибка при инициализации интерфейса:', error);
    } finally {
        document.getElementById("preloader").style.display = "none";
        document.getElementById("mainContent").style.display = "block";
    }
}


function CopyParserIdAndPassword(){
    const parserId = document.querySelector(".ParserIdValueText").textContent.trim();
    const password = document.querySelector(".PasswordValueText").textContent.trim();
    const text = parserId + " " + password;
    navigator.clipboard.writeText(text);
}

function openModal(type) {
	 const isMobile = window.innerWidth <= 768;
    currentModal = type;

    if (type === 'groups' && isMobile) {
       
        const sheet = document.getElementById('bottomSheet');
        const sheetLabel = document.getElementById('sheetLabel');
        const sheetContent = document.getElementById('sheetContent');

        if(window.targetGroups.length < 1){
            sheetLabel.textContent = "Выберите группы";
        } 
        else{
            sheetLabel.textContent = "Выбрано групп: " + window.targetGroups.length;
        }

        (window.userGroupsList ?? []).forEach(group => {
         
            const div = document.createElement('div');
            div.textContent = group;
            div.classList.add('sheet-item');
            if(window.targetGroups.includes(group)){
               div.classList.add('selected');
            }
            div.onclick = () => div.classList.toggle('selected');
            sheetContent.appendChild(div);
        });
        
        const saveBtn = document.getElementById('sheetSaveButton');
        sheet.classList.add('active');
        return; 
    }
	
     if (window.Telegram && window.Telegram.WebApp) {
        window.Telegram.WebApp.disableVerticalSwipes();
        window.Telegram.WebApp.expand();
    }
    document.getElementById("overlay").classList.add("active");
    document.getElementById("tagifyModal").classList.add("active");

    const label = document.getElementById("tagifyLabel");
    const input = document.getElementById("tagifyInput");
    const fileinput = document.getElementById("InputKeywordsFile");
    const labelForKeywordss = document.getElementById("labelForKeywords");

   if (type !== "keywords") {
        fileinput.style.display = "none";
        labelForKeywordss.style.display = "none";
    } else {
        fileinput.style.display = "block"; 
        labelForKeywordss.style.display = "block"; 

    }

    if (tagifyInstance) {
        try {
            tagifyInstance.destroy();
        } catch {}
        tagifyInstance = null;
    }
    input.value = "";

    tagifyInstance = new Tagify(input, {
        whitelist: type === 'groups' ? window.userGroupsList ?? [] : [],
        enforceWhitelist: type === 'groups',
        duplicates: false,
        delimiters: null,
        dropdown: {
            maxItems: 20,
            enabled: 0,
            closeOnSelect: false
        }
    });


    tagifyInstance.on('change', e => {
        const tags = tagifyInstance.value;
        console.log("Текущие теги:", tags.map(t => t.value));
        console.log("Количество тегов:", tags.length);

        const tagCountElem = document.getElementById('tagCount');
        if (tagCountElem) {
            tagCountElem.textContent = `Выбрано тегов: ${tags.length}`;
        }
        updateTagCounts(tags.length);
    });
  
    tagifyInstance.addTags(savedTags[type]);
}


function closeModal() {
    document.getElementById("overlay").classList.remove("active");
    document.getElementById("tagifyModal").classList.remove("active");
    const tagslabel = document.getElementById("tagCount");
    tagslabel.textContent = "Кол-во тегов: 0";

    if (tagifyInstance) tagifyInstance.destroy();
    tagifyInstance = null;
}

//Теги
function updateTagCounts(tagsLenght) {
  const tagslabel = document.getElementById("tagCount");
  tagslabel.textContent = `Кол-во тегов: ${tagsLenght}`
}

async function saveTags() {
    console.log(currentModal + " это нынешняя модалка");

    if (currentModal === 'keywords') {
        const tags = tagifyInstance ? tagifyInstance.value.map(item => item.value) : [];
        const combinedTags = [...tags];

        const fileInput = document.getElementById("InputKeywordsFile");
        const file = fileInput.files[0];

        if (file) {
            try {
                const text = await file.text();
                const lines = text
                    .split(/\r?\n/)
                    .map(line => line.trim())
                    .filter(line => line.length > 0);

                combinedTags.push(...lines);
            } catch (err) {
                console.error("Ошибка при чтении файла:", err);
                alert("Не удалось прочитать файл.");
                return;
            }
        }

        savedTags[currentModal] = combinedTags;
        console.log(`keywords:`, combinedTags);

        await fetch(`${config.API_BASE}/ParserConfig/AddParserKeywords`, {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(combinedTags)
        });

    } else if (currentModal === 'groups') {
        let tags = [];
        if (window.innerWidth <= 768) {
            const sheetContent = document.getElementById('sheetContent');
            tags = Array.from(sheetContent.querySelectorAll('.selected')).map(d => d.textContent);
        } else if (tagifyInstance) {
            tags = tagifyInstance.value.map(item => item.value);
        }

        savedTags[currentModal] = tags;

        console.log(`groups:`, tags);

        await fetch(`${config.API_BASE}/ParserConfig/AddGroupsToParser`, {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ groupNames: tags })
        });

        if (window.innerWidth <= 768) {
            document.getElementById('bottomSheet').classList.remove('active');
        } else {
            closeModal();
        }
    }
}

//Таймер
function stopTickTimer() {
    if (tickIntervalId !== null) {
        clearInterval(tickIntervalId);
        tickIntervalId = null;
    }
    const span = document.querySelector(".RemainingTimeToStopParser");
    if (span) {
        span.style.display = 'none';
    }
}

function startTickTimer(timeString) {
    if (timeString.includes('д')) {

        return;
    }
    
    stopTickTimer(); 
    
    const hoursMatch = timeString.match(/(\d+)ч/);
    const minutesMatch = timeString.match(/(\d+)м/);
    const secondsMatch = timeString.match(/(\d+)с/);
    
    let hours = hoursMatch ? parseInt(hoursMatch[1]) : 0;
    let minutes = minutesMatch ? parseInt(minutesMatch[1]) : 0;
    let seconds = secondsMatch ? parseInt(secondsMatch[1]) : 0;
    

    let span = document.querySelector(".RemainingTimeToStopParser");
    if (!span) {
        span = document.createElement("span");
        span.classList.add("RemainingTimeToStopParser");
        
        const buttonHandler = document.querySelector(".ParserButtonHandler");
        if (buttonHandler) {
            buttonHandler.appendChild(span);
        }
    }
    

    span.style.display = 'block'
    span.style.alignSelf = 'center';
    span.style.marginLeft = "10px";
    span.style.marginBottom = "10px";

    const btn = document.querySelector(".StartParserButton, .StopParserButton");
    if (btn && btn.parentNode) {
        btn.parentNode.insertBefore(span, btn.nextSibling);
    }

    const formattedStart = 
        String(hours).padStart(2, '0') + ":" +
        String(minutes).padStart(2, '0') + ":" +
        String(seconds).padStart(2, '0');
    span.textContent = formattedStart;

    tickIntervalId = setInterval(() => {
        seconds--;
        if (seconds < 0) {
            seconds = 59;
            minutes--;
        }
        if (minutes < 0) {
            minutes = 59;
            hours--; 
        }

        if (hours <= 0 && minutes <= 0 && seconds <= 0) {
            clearInterval(tickIntervalId);
            tickIntervalId = null;
            span.textContent = "00:00:00";
            return;
        }

        const formatted = 
            String(hours).padStart(2, '0') + ":" +
            String(minutes).padStart(2, '0') + ":" +
            String(seconds).padStart(2, '0');
        span.textContent = formatted;

    }, 1000);
}

//Выход с аккаунта
async function Logout() {
    try {
        await fetch(`${config.API_BASE}/ParserConfig/Logout`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include'
        });
        window.location.href = `${config.DefaultStartFileLocation}/index.html`;
    } catch (error) {
        console.error("Ошибка при выходе:", error);
        alert("Произошла ошибка при выходе");
    }
}

async function ValidateInputsAndTryStartParsing(){
 const isStart = parserTogglebutton.classList.contains('StartParserButton');
    if (isStart) {     
        if (savedTags.keywords.length === 0) {
            alert('Введите ключевые слова');
            return;
        }

        if (savedTags.groups.length === 0) {
            alert('Введите группы');
            return;
        }
        setInputsEnabled(false);          
        console.log("Пробуем начать парсинг");
        updateParserButton('wait');
        await startParsing();

      
    } else {
        updateParserButton('wait');
        await stopParsing();
        const span = document.querySelector(".RemainingTimeToStopParser");
        if (span) span.remove();
        stopTickTimer();
        setInputsEnabled(true);
        updateParserButton('start');
    }
}


function OpenSidePanel() 
{
    const sidePanel = document.getElementById('sidePanel');
    EnableOverlayOverlay(true);
    sidePanel.classList.remove('hidden');
    sidePanel.classList.add('active');
    document.body.style.overflow = 'hidden';
}

function EnableOverlayOverlay(overlayenabled) {
    if (overlayenabled) {
        const overlay = document.getElementById('overlay');
        overlay.classList.remove('hidden');
        overlay.classList.add('active');
    }
    else {
        const overlay = document.getElementById('overlay');
        overlay.classList.add('hidden');
        overlay.classList.remove('active');
    }
}


function closeSidePanel() {
    const sidePanel = document.getElementById('sidePanel');
    const tagifyModal = document.getElementById('tagifyModal');
    sidePanel.classList.remove('active');
    
    tagifyModal.classList.remove('active');
    tagifyModal.classList.add('hidden');
    EnableOverlayOverlay(false);
    setTimeout(() => {
        sidePanel.classList.add('hidden');
        overlay.classList.add('hidden');
        document.body.style.overflow = '';
    }, 300);
}


async function startParsing() {
    parserTogglebutton.disabled = true;
    try {
        const startResponse = await fetch(`${config.API_BASE}/ParserConfig/StartParsing`, {
            method: 'POST',
            credentials: 'include'
        });

        if (!startResponse.ok) {
            if (startResponse.status === 403) {
                throw new Error("Недостаточно времени подписки для запуска парсинга.");
            }

            const errorText = await startResponse.text();
            throw new Error("Ошибка запуска парсинга: " + errorText);
        }

        const remainTimeResponse = await fetch(`${config.API_BASE}/ParserConfig/GetParserRemainTime`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include'
        });

        if (!remainTimeResponse.ok) {
            const errorText = await remainTimeResponse.text();
            throw new Error("Ошибка получения оставшегося времени: " + errorText);
        }

        const remainTimeJson = await remainTimeResponse.json();
        const remainTime = remainTimeJson.remainingParsingTimeHoursMinutes;

        console.log(remainTimeJson);

        startTickTimer(remainTime);
        startSignalR();
        updateParserButton('stop');

    } catch (error) {
        console.error('Ошибка при запуске парсинга:', error);
        updateParserButton('start');
        alert('Ошибка при запуске парсинга: ' + error.message);
    } finally {
        parserTogglebutton.disabled = false;
        setInputsEnabled(true);
    }
}


async function stopParsing() {
    try {
        await fetch(`${config.API_BASE}/ParserConfig/StopParsing`, {
            method: 'POST',
            credentials: 'include'
        });
        stopTickTimer();
        parserTogglebutton.classList.remove('StopParserButton');
        parserTogglebutton.classList.add('StartParserButton');
        icon.src = 'Assets/StartParsingImage.png';
        icon.alt = 'Старт';
        text.textContent = 'Начать парсинг';
        setInputsEnabled(true);
    } catch (error) {
        console.error('Ошибка при остановке парсинга:', error);
        alert('Ошибка при остановке парсинга');
    }
}

function startSignalR() {
    if (connection && connection.state === "Connected") {
        console.log("SignalR уже подключен");
        return;
    }
    connection = new signalR.HubConnectionBuilder()
        .withUrl(`${config.API_BASE}/parserHub`, { withCredentials: true })
        .build();

    connection.on("ReceiveMessage", (data) => {
        console.log("Получено сообщение от SignalR:", data);
        addMessageToList(data);
    });
    connection.on("ParsingIsStoped", () => {
        setInputsEnabled(true);
        updateParserButton('start');
        console.log("попытка прошла");
    });
    connection.on("ParserChangedProxy", (remainingTime) => {
        updateParserButton('start'); 
        startTickTimer(remainingTime); 
        setInputsEnabled(false); 
    });
    connection.on("ParsingIsStoped", (data) => {
       console.log("Пришло от сервера:", data);
       updateTotalParsingTime(data.totalParsingTime); 
    });
    connection.start()
        .then(() => console.log("SignalR подключен"))
        .catch(err => console.error("Ошибка подключения SignalR:", err));
}

function updateTotalParsingTime(totalparsingtime) {
    const totalparsingTimeElement = document.getElementsByClassName("TotalParsingTimeValueText")[0];
    if (totalparsingTimeElement) {
        if (totalparsingtime === "") {
            totalparsingTimeElement.textContent = "0м";
        }
        else {
            totalparsingTimeElement.textContent = totalparsingtime;
        }
    } 
    else{
     console.log("элемент не нашло :((("); 
    }
}

function addMessageToList(data) {
    const container = document.querySelector(".DataList");
    const item = document.createElement("li");
    const noMessagesText = document.querySelector('.DataListDoNotHaveAnyMessagesText');

    if (noMessagesText) {
        noMessagesText.remove();
    }

    item.className = "UserParserDataItem";

    const safeMessageText = escapeHtml(data.messageText);
    const safeName = escapeHtml(data.name);
    const safeMessageTime = escapeHtml(data.messageTime);
    const localTime = new Date(safeMessageTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });

    const safeProfileImageUrl = escapeUrl(data.profileImageUrl || 'https://via.placeholder.com/100');
    const safeUsername = escapeHtml(data.username);

    const img = document.createElement('img');
    img.src = safeProfileImageUrl;
    img.alt = 'User Image';
    img.className = 'ListItemImage';

    const nicknameDiv = document.createElement('div');
    nicknameDiv.className = 'NicknameHandler';
    nicknameDiv.textContent = safeName;

    const messageTextDiv = document.createElement('div');
    messageTextDiv.className = 'MessageText';
    messageTextDiv.textContent = safeMessageText;

    const messageTimeDiv = document.createElement('div');
    messageTimeDiv.className = 'MessageTime';
    messageTimeDiv.textContent = localTime;

    const spamButton = document.createElement('button');
    spamButton.className = 'ThisIsSpamButton';
    spamButton.dataset.message = safeMessageText; 
    spamButton.textContent = 'Это спам';

    const telegramButton = document.createElement('button');
    telegramButton.className = 'GoToTelegramButton';
    telegramButton.dataset.username = safeUsername; 
    telegramButton.textContent = 'Чат';

    item.appendChild(img);
    item.appendChild(nicknameDiv);
    item.appendChild(messageTextDiv);
    item.appendChild(messageTimeDiv);
    item.appendChild(spamButton);
    item.appendChild(telegramButton);

    container.prepend(item);

    telegramButton.addEventListener('click', () => {
        const username = telegramButton.dataset.username;
        if (username) {
            window.open(`https://t.me/${username}`, '_blank');
        } else {
            alert('Telegram username не найден');
        }
    });
}


async function ThisIsASpam(spamMessage) {
    try {
        const response = await fetch(`${config.API_BASE}/ParserConfig/AddNewSpamMessage`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({
                Message: spamMessage
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        console.log('Success:', result);
        await RemoveAllSpamMessages(spamMessage);
    } catch (error) {
        console.error('Error:', error);
        alert('Ошибка при добавлении в спам');
    }
}

async function RemoveAllSpamMessages(spamMessage) {
    const items = document.querySelectorAll('.UserParserDataItem');
    
    items.forEach(item => {
        const messageElement = item.querySelector('.MessageText');
        if (messageElement && messageElement.textContent === spamMessage) {
            item.remove();
        }
    });
}
function updateParserButton(state) {
parserTogglebutton.classList.remove('StartParserButton', 'StopParserButton', 'WaitParserButton');    
    switch(state) {
        case 'start':
            parserTogglebutton.classList.add('StartParserButton');
            icon.src = 'Assets/StartParsingImage.png';
            icon.alt = 'Старт';
            text.textContent = 'Начать парсинг';
            break;
            
        case 'stop':
            parserTogglebutton.classList.add('StopParserButton');
            icon.src = 'Assets/StopParsingImage.png';
            icon.alt = 'Стоп';
            text.textContent = 'Остановить парсинг';
            break;
            
        case 'wait':
            parserTogglebutton.classList.add('WaitParserButton');
            icon.src = 'Assets/Loading.gif';
            icon.alt = 'Ожидание';
            text.textContent = 'Ожидание...';
            break;
    }
}

function setInputsEnabled(enabled) {
    const inputs = [
        AddGroupsButton,
        AddKeywordsButton
    ];
    inputs.forEach(input => {
        input.disabled = !enabled;
        input.style.cursor = enabled ? 'pointer' : 'not-allowed';
    });
}

document.addEventListener('click', async (e) => {
    if (e.target.classList.contains('ThisIsSpamButton')) {
        const spamMessage = e.target.dataset.message;
        await ThisIsASpam(spamMessage);
    }
});

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}
function escapeUrl(url) {
    try {
        return encodeURI(url);
    } catch {
        return 'https://via.placeholder.com/100';
    }
}
