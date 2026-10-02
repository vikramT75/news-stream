const messagesContainer = document.getElementById('chat-messages');
const inputField = document.getElementById('prompt-input');
const sendBtn = document.getElementById('send-btn');

inputField.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') sendMessage();
});
sendBtn.addEventListener('click', sendMessage);

async function sendMessage() {
    const prompt = inputField.value.trim();
    if (!prompt) return;

    appendMessage('user', prompt);
    inputField.value = '';
    inputField.disabled = true;
    sendBtn.disabled = true;

    const botMessageContent = appendMessage('bot', '');
    let fullBotMessage = '';

    try {
        const apiUrl = `http://${window.location.hostname}:5000/api/chat/stream`;
        const response = await fetch(apiUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ prompt: prompt })
        });

        if (!response.ok) throw new Error('API Error: ' + response.statusText);

        const reader = response.body.getReader();
        const decoder = new TextDecoder('utf-8');

        while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            const chunk = decoder.decode(value, { stream: true });
            const lines = chunk.split('\n');

            for (const line of lines) {
                if (line.startsWith('data: ')) {
                    const data = line.substring(6);
                    if (data === '[DONE]') break;
                    
                    // Format newline literals correctly if they're escaped by the backend
                    fullBotMessage += data.replace(/\\n/g, '\n');
                    
                    if (typeof marked !== 'undefined') {
                        botMessageContent.innerHTML = marked.parse(fullBotMessage);
                    } else {
                        botMessageContent.innerText = fullBotMessage;
                    }
                    
                    const isNearBottom = messagesContainer.scrollHeight - messagesContainer.scrollTop - messagesContainer.clientHeight < 100;
                    if (isNearBottom) {
                        messagesContainer.scrollTop = messagesContainer.scrollHeight;
                    }
                }
            }
        }
    } catch (error) {
        botMessageContent.innerHTML += `<br><span style="color:red; font-size:0.9em;">[Error connecting to API. Make sure backend is running.]</span>`;
    } finally {
        inputField.disabled = false;
        sendBtn.disabled = false;
        inputField.focus();
    }
}

function appendMessage(sender, text) {
    const div = document.createElement('div');
    div.className = `message ${sender}`;
    const header = sender === 'bot' ? '<strong>AI:</strong> ' : '<strong>You:</strong> ';
    const contentHtml = `<div class="msg-content">${text}</div>`;
    div.innerHTML = header + contentHtml;
    messagesContainer.appendChild(div);
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
    return div.querySelector('.msg-content');
}
