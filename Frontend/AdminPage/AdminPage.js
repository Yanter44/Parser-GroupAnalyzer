//Константы
const API_BASE = "http://localhost:5154";
const usersTableBody = document.getElementById('usersTableBody');
const searchInput = document.getElementById('searchInput');
const SetSubscriptionTypePanel = document.getElementsByClassName('SetSubscriptionTypePanel')[0];
const SetNewProxyPanel = document.getElementsByClassName('SetNewProxyPanel')[0];
const overlay = document.getElementsByClassName('overlayStyle')[0];
const token = localStorage.getItem("jwtToken");
//Подписки

document.addEventListener("DOMContentLoaded", InitializePage);

async function InitializePage(){
    if (!token) {
        window.location.href = "../AdminLogin/AdminLogin.html";
        return;
    }
    try {
        const validateResponse = await fetch(`${API_BASE}/AdminAuth/ValidateToken`, {
            method: 'GET',
            headers: { "Authorization": token }
        });

        if (!validateResponse.ok) {
            localStorage.removeItem("jwtToken");
            window.location.href = "../AdminLogin/AdminLogin.html";
            return;
        }

    const response = await fetch(`${API_BASE}/Admin/GetAllParsers`, {
      method: 'GET',
      headers: {"Authorization": token}
    });
    const result = await response.json();
    
    const tableBody = document.getElementById("usersTableBody");
    tableBody.innerHTML = ''; 
    
    result.forEach(element => {
      const row = document.createElement('tr');
      row.innerHTML = `
        <td>${element.tgNickname}</td>
        <td>${element.parserId}</td>
        <td>${element.password}</td>
        <td>${element.subscriptionRate}</td>
        <td>${element.totalParsingTime}</td>
        <td>${element.proxyAddress}</td>
        <td>
            <button class="SetSubscriptionTypeButton" onclick="OpenSetSubscriptionTypePanel('${element.parserId.replace(/'/g, "\\'")}')">Изменить тип подписки</button>
            <button class="SetProxyButton" data-userparserid="${element.parserId}" onclick="OpenSetNewProxyPanel('${element.parserId.replace(/'/g, "\\'")}')">Установить прокси</button>
            <button class="DeleteUserButton" data-userparserid="${element.parserId}" onclick="DeleteUser(this)">Удалить</button>
        </td>
      `;
      tableBody.appendChild(row);
    });
  }
  catch(error) {
    console.log(error);
  }
  finally { 
    document.getElementById("preloader").style.display = "none";
  }
}

function OpenSetSubscriptionTypePanel(parserId) {
    const panel = document.querySelector('.SetSubscriptionTypePanel');
    if (!panel) return;
    
    panel.dataset.currentParser = parserId;
   if (!SetSubscriptionTypePanel) {
        console.error('Элемент не найден');
        return;
    }
    if (SetSubscriptionTypePanel.style.display === 'none' || !SetSubscriptionTypePanel.style.display) {
        SetSubscriptionTypePanel.style.display = 'block';
        overlay.style.display = 'block';
    } else {
        SetSubscriptionTypePanel.style.display = 'none';
    }
}

function OpenSetNewProxyPanel(parserId){
    const panel = document.querySelector('.SetNewProxyPanel');
    if (!panel) return;
    
    panel.dataset.currentParser = parserId;
   if (!SetNewProxyPanel) {
        console.error('Элемент не найден');
        return;
    }
    if (SetNewProxyPanel.style.display === 'none' || !SetNewProxyPanel.style.display) {
        SetNewProxyPanel.style.display = 'block';
        overlay.style.display = 'block';
    } else {
        SetNewProxyPanel.style.display = 'none';
    }
}
async function SetNewProxy() {
    const panel = document.querySelector('.SetNewProxyPanel');
    const button = panel.querySelector('.ConfirmButton');
    const waitImage = panel.querySelector('.ConfirmButtonWaitImage');
    const proxyInput = panel.querySelector('#SetNewProxyInput');
    
    try {
        button.disabled = true;
        button.style.background = '#cccccc';   
        button.textContent = 'Ожидание...';
        
        const parserId = panel.dataset.currentParser;
        const setNewProxyModel = {
            ParserId: parserId,
            ProxyAdress: proxyInput.value 
        };
        
        const response = await fetch(`${API_BASE}/Admin/SetNewProxy`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': token
            },
            body: JSON.stringify(setNewProxyModel)
        });
        
        if(response.ok) {
            CloseAllPanels();
            InitializePage();
        } else {
            alert("Ошибка при изменении прокси");
        }
    } catch(error) {
        console.log(error);
        alert("Произошла ошибка: " + error.message);
    } finally {
        button.disabled = false;
        button.style.background = '#4A9FF5'; 
        button.textContent = 'Подтвердить';
    }
}

function CloseAllPanels(){
  SetSubscriptionTypePanel.style.display = 'none';
  SetNewProxyPanel.style.display = 'none';
  overlay.style.display = 'none';
}

async function ConfirmSubscriptionType() {
    const panel = document.querySelector('.SetSubscriptionTypePanel');
    const button = panel.querySelector('.ConfirmButton');
    const subscriptionType = document.getElementById('SubscriptionTypeSelect').value;
    const daysSubscription = parseInt(document.getElementById('SubscriptionDaysInput').value);
    try {
        button.disabled = true;
        button.style.backgroundColor = '#cccccc';
        button.innerHTML = 'Ожидание... <img class="ConfirmButtonWaitImage" src="./Assets/Loading.gif"/>';
        
        const parserId = panel.dataset.currentParser;

        const SetsubscriptionTypeModel = {
            ParserId: parserId,
            SubscriptionType: subscriptionType,
            DaysSubscription: daysSubscription
        };
        
        const response = await fetch(`${API_BASE}/Admin/SetSubscriptionType`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': token
            },
            body: JSON.stringify(SetsubscriptionTypeModel)
        });
        
        if(!response.ok) {
            throw new Error('Ошибка сервера: ' + response.status);
        }   
        CloseAllPanels();
        InitializePage();
    } catch (error) {
        console.error("Ошибка:", error);
        alert(error.message);
    } finally {
        if (button) {
            button.disabled = false;
            button.style.background = '#4A9FF5'; 
            button.textContent = 'Подтвердить';
        }
    }
}
async function DeleteUser(buttonElement) {
    const parserId = buttonElement.dataset.userparserid;
    
    if (!parserId) {
        console.error('ParserId не найден');
        return;
    }

    if (!confirm('Вы уверены, что хотите удалить этого пользователя?')) {
        return;
    }

    try {
        const response = await fetch(`${API_BASE}/Admin/DeleteUserAndParser`, {
            method: 'DELETE',
             headers: {
                'Content-Type': 'application/json',
                'Authorization': token
             },
            body: JSON.stringify({ parserId: parserId })
        });

        if (!response.ok) {
            throw new Error(`Ошибка: ${response.status}`);
        }
        if(response.ok){
           alert('Пользователь успешно удален');
           InitializePage();
        }

    } catch (error) {
        console.error("Ошибка при удалении:", error);
        alert("Не удалось удалить пользователя: " + error.message);
    }
}
