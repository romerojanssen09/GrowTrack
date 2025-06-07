/**
 * Account Status Checker
 * Periodically checks if the user's account status has changed
 * and logs them out if their account has been suspended or deactivated.
 */

(function () {
    // Only run for authenticated users
    if (!document.body.classList.contains('authenticated-user')) {
        return;
    }

    const CHECK_INTERVAL = 60000; // Check every minute
    let checkTimer = null;

    function startStatusChecker() {
        // Clear any existing timer
        if (checkTimer) {
            clearInterval(checkTimer);
        }

        // Set up periodic checking
        checkTimer = setInterval(checkAccountStatus, CHECK_INTERVAL);

        // Also check immediately on page load
        checkAccountStatus();
    }

    function checkAccountStatus() {
        fetch('/Login/CheckAccountStatus')
            .then(response => response.json())
            .then(data => {
                if (!data.valid) {
                    // Account is no longer valid, show message and redirect to logout
                    showAccountStatusMessage(data.reason);
                    setTimeout(() => {
                        window.location.href = '/Login/Logout';
                    }, 5000); // Give user 5 seconds to read the message
                }
            })
            .catch(error => {
                console.error('Error checking account status:', error);
            });
    }

    function showAccountStatusMessage(message) {
        // Create modal or notification to show the message
        if (typeof Swal !== 'undefined') {
            // Use SweetAlert2 if available
            Swal.fire({
                title: 'Account Status Changed',
                text: message,
                icon: 'warning',
                showConfirmButton: false,
                timer: 5000,
                timerProgressBar: true
            });
        } else {
            // Fallback to alert
            alert('Account Status Changed: ' + message + '\n\nYou will be logged out in 5 seconds.');
        }
    }

    // Start the status checker when the page is fully loaded
    if (document.readyState === 'complete') {
        startStatusChecker();
    } else {
        window.addEventListener('load', startStatusChecker);
    }
})();
