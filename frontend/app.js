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

    const botMessageDiv = appendMessage('bot', '');

    try {
        const response = await fetch('http://localhost:5268/api/chat/stream', {
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
                    
                    botMessageDiv.innerHTML += data;
                    messagesContainer.scrollTop = messagesContainer.scrollHeight;
                }
            }
        }
    } catch (error) {
        botMessageDiv.innerHTML += `<br><span style="color:red; font-size:0.9em;">[Error connecting to API. Make sure backend is running.]</span>`;
    } finally {
        inputField.disabled = false;
        sendBtn.disabled = false;
        inputField.focus();
    }
}

function appendMessage(sender, text) {
    const div = document.createElement('div');
    div.className = `message ${sender}`;
    div.innerHTML = sender === 'bot' ? `<strong>AI:</strong> ${text}` : `<strong>You:</strong> ${text}`;
    messagesContainer.appendChild(div);
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
    return div;
}
