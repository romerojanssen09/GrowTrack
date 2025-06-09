// Format option for select2 dropdowns
function formatOption(option) {
    if (!option.id) {
        return option.text;
    }
    return $('<span><i class="fas fa-check-circle me-2"></i>' + option.text + '</span>');
}

document.addEventListener('DOMContentLoaded', function() {
    // Initialize calendar
    var calendarEl = document.getElementById('calendar');
    var tasks = []; // Will be loaded from the database
    var staffCache = {}; // Cache for storing staff info

    // Set today's date as default in the date picker
    var today = new Date();
    var todayStr = today.toISOString().split('T')[0];
    document.getElementById('taskDate').value = todayStr;

    // Initialize select2 with pre-loaded data
    $('.select2-with-search').select2({
        placeholder: "Search and select...",
        allowClear: true,
        width: '100%',
        language: {
            noResults: function() {
                return "No results found";
            },
            searching: function() {
                return "Searching...";
            }
        },
        templateResult: formatOption,
        templateSelection: formatOption
    });

    if (role === 'Admin') {
        $('#boSharingOptions').hide();
        initializeBusinessCategories();
        initializeBusinessOwners();
        
        // When business categories change, update business owners list - removed select2 trigger
        $('#categoryCheckboxList').on('change', 'input[type="checkbox"]', function() {
            updateBusinessOwnersList();
        });
    } else if (role === 'BusinessOwner') {
        $('#adminSharingOptions').hide();
        initializeStaffMembers();
    } else if (role === 'Staff' && canAccess) {
        $('#adminSharingOptions').hide();
        $('#isAllContainer').show();
        initializeStaffMembers();
    } else {
        $('#isAllContainer, #adminSharingOptions, #boSharingOptions').hide();
    }

    function initializeBusinessCategories() {
        console.log('Initializing business categories from ViewBag data');
        
        // Clear existing category list
        const categoryCheckboxList = document.getElementById('categoryCheckboxList');
        if (categoryCheckboxList) {
            categoryCheckboxList.innerHTML = '';
            
        if (businessCategories && businessCategories.length > 0) {
                // Generate checkboxes for each business category

                businessCategories.forEach((category) => {
                    const categoryName = category.name;
                    const categoryId = category.id;
                    console.log('Category:', categoryName, categoryId, category);
                    const categoryItem = document.createElement('div');
                    categoryItem.className = 'form-check category-item mb-1';
                    
                    const checkbox = document.createElement('input');
                    checkbox.type = 'checkbox';
                    checkbox.className = 'form-check-input category-checkbox';
                    checkbox.id = `category-${categoryId}`;
                    // Use index+1 as the value (numeric ID) instead of the category name string
                    checkbox.value = categoryId;
                    checkbox.setAttribute('data-name', categoryName);
                    
                    const label = document.createElement('label');
                    label.className = 'form-check-label';
                    label.htmlFor = `category-${categoryId}`;
                    label.textContent = categoryName;
                    
                    categoryItem.appendChild(checkbox);
                    categoryItem.appendChild(label);
                    categoryCheckboxList.appendChild(categoryItem);
                });
            } else {
                categoryCheckboxList.innerHTML = '<div class="text-center py-2">No business categories found</div>';
            }
        }
        
        // Add search functionality
        const categorySearch = document.getElementById('categorySearch');
        if (categorySearch) {
            categorySearch.addEventListener('input', function() {
                const searchTerm = this.value.toLowerCase();
                document.querySelectorAll('.category-item').forEach(item => {
                    const categoryName = item.querySelector('label').textContent.toLowerCase();
                    if (categoryName.includes(searchTerm)) {
                        item.style.display = '';
                    } else {
                        item.style.display = 'none';
                    }
                });
            });
        }
    }

    function initializeBusinessOwners() {
        // Initial population of all business owners
        updateBusinessOwnersList();
        
        // Add search functionality
        const businessOwnerSearch = document.getElementById('businessOwnerSearch');
        if (businessOwnerSearch) {
            businessOwnerSearch.addEventListener('input', function() {
                const searchTerm = this.value.toLowerCase();
                document.querySelectorAll('.business-owner-item').forEach(item => {
                    const ownerName = item.querySelector('label').textContent.toLowerCase();
                    if (ownerName.includes(searchTerm)) {
                        item.style.display = '';
                    } else {
                        item.style.display = 'none';
                    }
                });
            });
        }
    }

    function updateBusinessOwnersList() {
        console.log('Updating business owners list based on selected categories');
        
        // Get selected categories
        const selectedCategories = [];
        document.querySelectorAll('#categoryCheckboxList input[type="checkbox"]:checked').forEach(checkbox => {
            selectedCategories.push(checkbox.getAttribute('data-name'));
        });
        
        console.log('Selected categories:', selectedCategories);
        
        // Clear existing business owner list
        const businessOwnerCheckboxList = document.getElementById('businessOwnerCheckboxList');
        if (businessOwnerCheckboxList) {
            businessOwnerCheckboxList.innerHTML = '';
            
            // Always show all business owners regardless of selected categories
            if (businessOwners && businessOwners.length > 0) {
                console.log('Generating checkboxes for business owners:', businessOwners.length);
                
                // Generate checkboxes for each business owner
                businessOwners.forEach(owner => {
                    const ownerItem = document.createElement('div');
                    ownerItem.className = 'form-check business-owner-item mb-1';
                    
                    const checkbox = document.createElement('input');
                    checkbox.type = 'checkbox';
                    checkbox.className = 'form-check-input business-owner-checkbox';
                    
                    // Make sure the ID uses the exact same format for owner IDs
                    // Force it to be a string to ensure consistent comparison
                    const ownerId = owner.id.toString();
                    checkbox.id = `owner-${ownerId}`;
                    checkbox.value = ownerId;
                    
                    // Debug
                    console.log(`Creating owner checkbox: id=${checkbox.id}, value=${checkbox.value}`);
                    
                    checkbox.setAttribute('data-name', owner.name);
                    checkbox.setAttribute('data-category', owner.category);
                    
                    const label = document.createElement('label');
                    label.className = 'form-check-label';
                    label.htmlFor = `owner-${ownerId}`;
                    
                    // Create styled spans for business name and owner's personal name
                    const businessNameSpan = document.createElement('span');
                    businessNameSpan.className = 'business-owner-name';
                    businessNameSpan.textContent = owner.name;
                    
                    const personalNameSpan = document.createElement('span');
                    personalNameSpan.className = 'business-owner-personal';
                    personalNameSpan.textContent = ` (${owner.firstName || ''} ${owner.lastName || ''})`.trim();
                    
                    // Highlight owners from selected categories
                    if (selectedCategories.length > 0 && selectedCategories.includes(owner.category)) {
                        // Add a visual indicator for owners in selected categories
                        businessNameSpan.innerHTML = `<i class="fas fa-check-circle text-success me-1" id="check-icon"></i>${owner.name}`;
                        businessNameSpan.style.fontWeight = 'bold';
                        ownerItem.style.backgroundColor = '#f8fff8';
                    }
                    
                    // Add both spans to the label
                    label.appendChild(businessNameSpan);
                    label.appendChild(personalNameSpan);
                    
                    // Add category badge if any categories are selected
                    if (selectedCategories.length > 0) {
                        const categoryBadge = document.createElement('span');
                        categoryBadge.className = 'badge bg-light text-secondary ms-1 small';
                        categoryBadge.textContent = owner.category;
                        categoryBadge.id = 'category-badge';
                        label.appendChild(categoryBadge);
                    }
                    
                    ownerItem.appendChild(checkbox);
                    ownerItem.appendChild(label);
                    businessOwnerCheckboxList.appendChild(ownerItem);
                });
            } else {
                businessOwnerCheckboxList.innerHTML = '<div class="text-center py-2">No business owners found</div>';
            }
        }
    }

    function initializeStaffMembers() {
        console.log('Initializing staff members from ViewBag data');
        
        // Clear existing staff list
        const staffCheckboxList = document.getElementById('staffCheckboxList');
        if (staffCheckboxList) {
            staffCheckboxList.innerHTML = '';
            
            if (staffMembers && staffMembers.length > 0) {
                // Generate checkboxes for each staff member
                staffMembers.forEach(staff => {
                    const staffItem = document.createElement('div');
                    staffItem.className = 'form-check staff-item mb-1';
                    
                    const checkbox = document.createElement('input');
                    checkbox.type = 'checkbox';
                    checkbox.className = 'form-check-input staff-checkbox';
                    checkbox.id = `staff-${staff.id}`;
                    checkbox.value = staff.id;
                    checkbox.setAttribute('data-name', staff.name);
                    
                    const label = document.createElement('label');
                    label.className = 'form-check-label';
                    label.htmlFor = `staff-${staff.id}`;
                    label.textContent = staff.name;
                    
                    staffItem.appendChild(checkbox);
                    staffItem.appendChild(label);
                    staffCheckboxList.appendChild(staffItem);
                });
            } else {
                staffCheckboxList.innerHTML = '<div class="text-center py-2">No staff members found</div>';
            }
        }
        
        // Add search functionality
        const staffSearch = document.getElementById('staffSearch');
        if (staffSearch) {
            staffSearch.addEventListener('input', function() {
                const searchTerm = this.value.toLowerCase();
                document.querySelectorAll('.staff-item').forEach(item => {
                    const staffName = item.querySelector('label').textContent.toLowerCase();
                    if (staffName.includes(searchTerm)) {
                        item.style.display = '';
                    } else {
                        item.style.display = 'none';
                    }
                });
            });
        }
    }

    var calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: 'dayGridMonth',
        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: 'dayGridMonth,timeGridWeek,timeGridDay'
        },
        editable: false,
        selectable: false,
        events: [], // Will be populated after loading tasks
        eventClick: function(info) {
            // This allows all users (including staff) to view task details
            console.log('Event clicked:', info.event);
            console.log('Event extendedProps:', info.event.extendedProps);
            console.log('Event taskId:', info.event.extendedProps.taskId, 'type:', typeof info.event.extendedProps.taskId);
            
            // Make sure the taskId is properly set
            if (!info.event.extendedProps.taskId) {
                console.error('Event clicked but taskId is missing in extendedProps');
                return;
            }
            
            // Force numeric taskId
            if (typeof info.event.extendedProps.taskId === 'string') {
                info.event.setExtendedProp('taskId', parseInt(info.event.extendedProps.taskId));
                console.log('Converted string taskId to number:', info.event.extendedProps.taskId);
            }
            
            showTaskDetails(info.event);
        },
        eventContent: function(arg) {
            // Custom event content with priority indicator
            let priorityDot = '';
            
            // Convert priority enum to string if needed
            const priorityString = getPriorityString(arg.event.extendedProps.priority);
            
            if (priorityString === 'High') {
                priorityDot = '<i class="fas fa-circle text-danger" style="font-size: 0.5rem; margin-right: 3px;"></i>';
            } else if (priorityString === 'Medium') {
                priorityDot = '<i class="fas fa-circle text-warning" style="font-size: 0.5rem; margin-right: 3px;"></i>';
            } else if (priorityString === 'Low') {
                priorityDot = '<i class="fas fa-circle text-success" style="font-size: 0.5rem; margin-right: 3px;"></i>';
            }

            return {
                html: `<div style="overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">${priorityDot}${arg.event.title}</div>`
            };
        }
    });

    calendar.render();

    // Initialize SignalR connection - moved here after calendar is initialized
    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/realTimeHub")
        .withAutomaticReconnect()
        .build();

    // Start SignalR connection
    connection.start().then(function() {
        console.log("SignalR Connected");
    }).catch(function(err) {
        console.error("SignalR Connection Error: " + err.toString());
        Swal.fire({
            icon: 'error',
            title: 'Connection Error',
            text: 'Failed to connect to real-time service. Some features may not work correctly.',
            toast: true,
            position: 'top-end',
            showConfirmButton: false,
            timer: 3000
        });
    });

    // Handle receiving a new appointment
    connection.on("ReceiveAppointment", function(appointment) {
        console.log("Received new appointment via SignalR:", appointment);
        
        // Only add if we don't already have this appointment
        if (!tasks.some(t => t.id === appointment.Id)) {
            // Normalize the appointment data to match client-side format (camelCase)
            const normalizedAppointment = {
                id: appointment.Id,
                title: appointment.Title,
                date: appointment.Date,
                time: appointment.Time,
                endDate: appointment.EndDate,
                endTime: appointment.EndTime,
                priority: appointment.Priority,
                notes: appointment.Notes,
                isAll: appointment.IsAll,
                boViewers: appointment.BOViewers,
                adminViewers1: appointment.AdminViewers1,
                adminViewers2: appointment.AdminViewers2,
                userId: appointment.UserId,
                whoSetAppointment: appointment.WhoSetAppointment || 0,
                isCompleted: appointment.IsCompleted || false,
                staffNameWhoCompleted: appointment.StaffNameWhoCompleted
            };
            
            // Add to tasks array
            tasks.push(normalizedAppointment);
            console.log('Added appointment via SignalR, tasks array now has', tasks.length, 'tasks');
            
            // Format the start datetime
            let eventStart;
            if (appointment.Time) {
                eventStart = `${appointment.Date}T${appointment.Time}`;
            } else {
                eventStart = appointment.Date;
            }
            
            // Format the end datetime if available
            let eventEnd;
            if (appointment.EndDate && appointment.EndTime) {
                eventEnd = `${appointment.EndDate}T${appointment.EndTime}`;
            } else if (appointment.EndDate) {
                eventEnd = appointment.EndDate;
            } else if (appointment.Date) {
                // If no end date is provided, use the start date
                eventEnd = appointment.Date;
            }
            
            // Get the priority color
            const priorityColor = getPriorityColor(appointment.Priority);
            
            // Create the event object
            const newEvent = {
                id: appointment.Id.toString(),
                title: appointment.Title,
                start: eventStart,
                end: eventEnd,
                allDay: !appointment.Time,
                color: priorityColor,
                extendedProps: {
                    taskId: parseInt(appointment.Id),
                    completed: appointment.IsCompleted || false,
                    priority: appointment.Priority,
                    priorityString: getPriorityString(appointment.Priority),
                    notes: appointment.Notes || '',
                    isPast: false,
                    date: appointment.Date,
                    time: appointment.Time,
                    endDate: appointment.EndDate,
                    endTime: appointment.EndTime,
                    isAll: appointment.IsAll,
                    boViewers: appointment.BOViewers,
                    adminViewers1: appointment.AdminViewers1,
                    adminViewers2: appointment.AdminViewers2,
                    userId: appointment.UserId,
                    userIdString: appointment.UserIdString,
                    user: appointment.User,
                    whoSetAppointment: appointment.WhoSetAppointment || 0,
                    staffNameWhoCompleted: appointment.StaffNameWhoCompleted
                }
            };
            
            console.log('Adding event to calendar via SignalR:', newEvent);
            
            try {
                // Add the event to the calendar
                calendar.addEvent(newEvent);
                
                // Force calendar to refresh
                calendar.refetchEvents();
                calendar.render();
                
                console.log('Calendar events after adding:', calendar.getEvents().length);
                
                // Show notification toast
                Swal.fire({
                    icon: 'success',
                    title: 'New Appointment',
                    text: `New appointment added: ${appointment.Title}`,
                    toast: true,
                    position: 'top-end',
                    showConfirmButton: false,
                    timer: 3000
                });
            } catch (error) {
                console.error('Error adding event to calendar:', error);
            }
        }
    });

    // Handle appointment updates
    connection.on("UpdateAppointment", function(appointment) {
        console.log("Received appointment update via SignalR:", appointment);
        
        // Find and remove the existing appointment
        const existingIndex = tasks.findIndex(t => t.id === appointment.Id);
        if (existingIndex !== -1) {
            tasks.splice(existingIndex, 1);
        }
        
        // Remove from calendar
        cleanupTaskEvents(appointment.Id);
        
        // Normalize the appointment data to match client-side format (camelCase)
        const normalizedAppointment = {
            id: appointment.Id,
            title: appointment.Title,
            date: appointment.Date,
            time: appointment.Time,
            endDate: appointment.EndDate,
            endTime: appointment.EndTime,
            priority: appointment.Priority,
            notes: appointment.Notes,
            isAll: appointment.IsAll,
            boViewers: appointment.BOViewers,
            adminViewers1: appointment.AdminViewers1,
            adminViewers2: appointment.AdminViewers2,
            userId: appointment.UserId,
            whoSetAppointment: appointment.WhoSetAppointment || 0,
            isCompleted: appointment.IsCompleted || false,
            staffNameWhoCompleted: appointment.StaffNameWhoCompleted
        };
        
        // Add to tasks array
        tasks.push(normalizedAppointment);
        
        // Format the start datetime
        let eventStart;
        if (appointment.Time) {
            eventStart = `${appointment.Date}T${appointment.Time}`;
        } else {
            eventStart = appointment.Date;
        }
        
        // Format the end datetime if available
        let eventEnd;
        if (appointment.EndDate && appointment.EndTime) {
            eventEnd = `${appointment.EndDate}T${appointment.EndTime}`;
        } else if (appointment.EndDate) {
            eventEnd = appointment.EndDate;
        } else if (appointment.Date) {
            // If no end date is provided, use the start date
            eventEnd = appointment.Date;
        }
        
        // Get the priority color
        const priorityColor = getPriorityColor(appointment.Priority);
        
        // Create the event object
        const updatedEvent = {
            id: appointment.Id.toString(),
            title: appointment.Title,
            start: eventStart,
            end: eventEnd,
            allDay: !appointment.Time,
            color: priorityColor,
            extendedProps: {
                taskId: parseInt(appointment.Id),
                completed: appointment.IsCompleted || false,
                priority: appointment.Priority,
                priorityString: getPriorityString(appointment.Priority),
                notes: appointment.Notes || '',
                isPast: false,
                date: appointment.Date,
                time: appointment.Time,
                endDate: appointment.EndDate,
                endTime: appointment.EndTime,
                isAll: appointment.IsAll,
                boViewers: appointment.BOViewers,
                adminViewers1: appointment.AdminViewers1,
                adminViewers2: appointment.AdminViewers2,
                userId: appointment.UserId,
                userIdString: appointment.UserIdString,
                user: appointment.User,
                whoSetAppointment: appointment.WhoSetAppointment || 0,
                staffNameWhoCompleted: appointment.StaffNameWhoCompleted
            }
        };
        
        console.log('Adding updated event to calendar via SignalR:', updatedEvent);
        
        try {
            // Add the event to the calendar
            calendar.addEvent(updatedEvent);
            
            // Force calendar to refresh
            calendar.refetchEvents();
            calendar.render();
            
            console.log('Calendar events after updating:', calendar.getEvents().length);
            
            // Show notification toast
            Swal.fire({
                icon: 'info',
                title: 'Appointment Updated',
                text: `Appointment updated: ${appointment.Title}`,
                toast: true,
                position: 'top-end',
                showConfirmButton: false,
                timer: 3000
            });
        } catch (error) {
            console.error('Error updating event in calendar:', error);
        }
    });

    // Handle appointment deletions
    connection.on("DeleteAppointment", function(appointmentId) {
        console.log("Received appointment deletion via SignalR:", appointmentId);
        
        // Find the appointment to get its title for the notification
        const appointmentToDelete = tasks.find(t => t.id === appointmentId);
        let appointmentTitle = appointmentToDelete ? appointmentToDelete.title : "Appointment";
        
        // Remove from tasks array
        tasks = tasks.filter(t => t.id !== appointmentId);
        
        try {
            // Remove from calendar
            cleanupTaskEvents(appointmentId);
            
            // Force calendar to refresh
            calendar.refetchEvents();
            calendar.render();
            
            console.log('Calendar events after deletion:', calendar.getEvents().length);
            
            // Show notification toast
            Swal.fire({
                icon: 'warning',
                title: 'Appointment Deleted',
                text: `${appointmentTitle} has been deleted`,
                toast: true,
                position: 'top-end',
                showConfirmButton: false,
                timer: 3000
            });
        } catch (error) {
            console.error('Error removing event from calendar:', error);
        }
    });

    // Load tasks from the database
    loadTasks();
    
    // Initialize the task modal
    var taskModal = new bootstrap.Modal(document.getElementById('taskModal'));
    
    // Initialize the task details modal with a null check
    const taskDetailsModalElement = document.getElementById('taskDetailsModal');
    var taskDetailsModal = taskDetailsModalElement ? new bootstrap.Modal(taskDetailsModalElement) : null;
    
    // Set up the event handlers for modal buttons
    document.getElementById('openAddTaskModal').addEventListener('click', function() {
        console.log("openAddTaskModal is clicked!");
        resetTaskForm();
        document.getElementById('taskModalTitle').textContent = 'Add New Appointment';
        document.getElementById('saveTask').setAttribute('data-mode', 'add');
        
        // Clear all staff checkboxes when opening a new task form
        document.querySelectorAll('#staffCheckboxList input[type="checkbox"]').forEach(checkbox => {
            checkbox.checked = false;
        });
        
        taskModal.show();
    });
    
    document.getElementById('saveTask').addEventListener('click', function() {
        // Prevent double-clicking by disabling the button
        const saveBtn = this;
        saveBtn.disabled = true;
        
        // Show loading state
        const originalText = saveBtn.innerHTML;
        saveBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Saving...';
        
        var mode = this.getAttribute('data-mode');
        if (mode === 'add') {
            addTask(saveBtn, originalText);
        } else {
            updateTask(parseInt(document.getElementById('taskId').value), saveBtn, originalText);
        }
    });

    function loadTasks() {
        console.log('Starting to load tasks from server...');
        fetch('/Calendar/GetTasks')
            .then(async response => {
                console.log('Response received from server, status:', response.status);
                if (!response.ok) {
                    const errorData = await response.json().catch(() => null);
                    throw new Error(errorData?.message || `HTTP error! status: ${response.status}`);
                }
                return response.json();
            })
            .then(data => {
                console.log('Tasks data received from server:', data);
                // Ensure all task priorities are properly converted from string to number
                tasks = data.map(task => {
                    if (typeof task.priority === 'string' && !isNaN(task.priority)) {
                        task.priority = parseInt(task.priority);
                    }
                    
                    // Ensure userId is properly set (may come as undefined from server)
                    if (task.userId === undefined && task.userIdString) {
                        task.userId = parseInt(task.userIdString);
                    }
                    
                    // Log userId data for debugging
                    console.log(`Task ${task.id} userId: ${task.userId}, userIdString: ${task.userIdString}`);
                    
                    return task;
                });
                console.log('Tasks loaded from server, count:', tasks.length);
                
                // Log the first task if available to see its structure
                if (tasks.length > 0) {
                    console.log('Sample task object structure:', tasks[0]);
                    console.log('Task properties:', Object.keys(tasks[0]).join(', '));
                } else {
                    console.log('No tasks returned from server');
                }
                
                // Clear existing events
                const existingEvents = calendar.getEvents();
                console.log('Clearing', existingEvents.length, 'existing events from calendar');
                existingEvents.forEach(event => event.remove());
                
                // Current date for determining past events
                const now = new Date();
                const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
                
                // Add events to calendar - show ALL events (both past and future)
                tasks.forEach(task => {
                    console.log('Adding task to calendar:', task.id, task.title, task.date, task.time);
                    
                    // Determine if the task is in the past
                    const taskDate = new Date(task.date);
                    let isPast = taskDate < today;
                    
                    // For tasks today, check if the time has passed
                    if (!isPast && taskDate.getTime() === today.getTime() && task.time) {
                        const [hours, minutes] = formatTime(task.time).split(':').map(Number);
                        const taskTime = new Date();
                        taskTime.setHours(hours, minutes, 0, 0);
                        isPast = taskTime < now;
                    }
                    
                    // Get the string representation of priority for display
                    const priorityString = getPriorityString(task.priority);
                    
                    // Determine appropriate class based on past status
                    let eventClassNames = [];
                    if (isPast) {
                        eventClassNames.push('past-event');
                    }
                    
                    // Format start date/time
                    let eventStart;
                    if (task.time) {
                        eventStart = `${task.date}T${formatTime(task.time)}`;
                    } else {
                        eventStart = task.date;
                    }
                    
                    // Format end date/time if available
                    let eventEnd;
                    if (task.endDate && task.endTime) {
                        eventEnd = `${task.endDate}T${formatTime(task.endTime)}`;
                    } else if (task.endDate) {
                        eventEnd = task.endDate;
                    } else if (task.date) {
                        // If no end date is provided, use the start date
                        eventEnd = task.date;
                    }
                    
                    console.log('Creating event with start:', eventStart, 'and end:', eventEnd);
                    
                    const calendarEvent = {
                        id: task.id.toString(), // Ensure consistent ID format as string
                        title: task.title,
                        start: eventStart,
                        end: eventEnd,
                        allDay: !task.time,
                        extendedProps: {
                            taskId: task.id,
                            priority: task.priority,
                            priorityString: priorityString,
                            notes: task.notes || '',
                            isPast: isPast,
                            // Add date/time properties
                            date: task.date,
                            time: task.time,
                            endDate: task.endDate,
                            endTime: task.endTime,
                            // Add sharing properties to extendedProps
                            isAll: task.isAll,
                            boViewers: task.boViewers,
                            adminViewers1: task.adminViewers1,
                            adminViewers2: task.adminViewers2,
                            userId: task.userId,
                            userIdString: task.userIdString,
                            user: task.user,
                            whoSetAppointment: task.whoSetAppointment
                        },
                        color: getPriorityColor(task.priority),
                        classNames: eventClassNames
                    };
                    
                    console.log('Adding event to calendar:', calendarEvent);
                    calendar.addEvent(calendarEvent);
                });
                
                // Refresh calendar to ensure events are rendered
                console.log('Refreshing calendar');
                calendar.refetchEvents();
            })
            .catch(error => {
                console.error('Error loading tasks:', error);
                Swal.fire({
                    icon: 'error',
                    title: 'Error Loading Appointments',
                    text: error.message || 'Failed to load appointments. Please try refreshing the page.',
                    confirmButtonText: 'OK'
                });
            });
    }

    function formatTime(timeObj) {
        // Convert TimeOnly object to string format HH:mm
        if (typeof timeObj === 'string') {
            return timeObj.substring(0, 5); // Extract HH:mm part
        }
        return `${timeObj.hours.toString().padStart(2, '0')}:${timeObj.minutes.toString().padStart(2, '0')}`;
    }
    
    function formatTimeForDisplay(timeStr) {
        // Convert HH:mm string to display format (e.g., "1:30 PM")
        if (!timeStr) return '';
        
        let [hours, minutes] = formatTime(timeStr).split(':');
        let intHours = parseInt(hours);
        let ampm = intHours >= 12 ? 'PM' : 'AM';
        intHours = intHours % 12;
        intHours = intHours ? intHours : 12; // Convert 0 to 12
        return `${intHours}:${minutes} ${ampm}`;
    }

    // Allow adding task with Enter key
    document.getElementById('taskTitle').addEventListener('keypress', function(e) {
        if (e.key === 'Enter') {
            addTask();
        }
    });

    // Toggle sharing options when IsAll is checked
    $('#isAllCheckbox').change(function() {
        if ($(this).is(':checked')) {
            $('#adminSharingOptions, #boSharingOptions').slideUp();
            // Clear selections when hiding
            if (role === 'Admin') {
                // Clear all category checkboxes
                document.querySelectorAll('#categoryCheckboxList input[type="checkbox"]').forEach(checkbox => {
                    checkbox.checked = false;
                });
                // Clear all business owner checkboxes
                document.querySelectorAll('#businessOwnerCheckboxList input[type="checkbox"]').forEach(checkbox => {
                    checkbox.checked = false;
                });
            } else if (role === 'BusinessOwner') {
                // Clear all staff checkboxes
                document.querySelectorAll('#staffCheckboxList input[type="checkbox"]').forEach(checkbox => {
                    checkbox.checked = false;
                });
            }
        } else {
            if (role === 'Admin') {
                $('#adminSharingOptions').slideDown();
            } else if (role === 'BusinessOwner') {
                $('#boSharingOptions').slideDown();
            }
        }
    });

    function loadStaffMembers() {
        fetch('/Calendar/GetStaff')
            .then(response => response.json())
            .then(data => {
                $('#staffMembers').empty();
                data.forEach(staff => {
                    $('#staffMembers').append(new Option(staff.Name, staff.Id));
                });
                $('#staffMembers').trigger('change');
            })
            .catch(error => {
                console.error('Error loading staff members:', error);
                showAlert('Failed to load staff members', 'danger');
            });
    }

    // Update the addTask function to use FormData instead of JSON for sending the data to the server
    function addTask(saveBtn, originalText) {
        try {
            // Validate required fields first
            if (!validateTaskForm()) {
                // Restore button on validation failure
                saveBtn.disabled = false;
                saveBtn.innerHTML = originalText;
                return;
            }
            
            // Create a FormData object
            const formData = new FormData();
            formData.append('__RequestVerificationToken', $('input[name="__RequestVerificationToken"]').val());
            
            // Map form fields directly to CalendarTaskDTO properties
            formData.append('Title', $('#taskTitle').val().trim());
            formData.append('Date', $('#taskDate').val());
            formData.append('EndDate', $('#taskEndDate').val() || $('#taskDate').val()); // Use start date if end date is not provided
            formData.append('Time', $('#taskTime').val() || null);
            formData.append('EndTime', $('#taskEndTime').val() || $('#taskTime').val()); // Use start time if end time is not provided
            
            // Ensure priority is a number
            const priorityValue = parseInt($('#taskPriority').val());
            formData.append('Priority', priorityValue);
            console.log('Sending priority value:', priorityValue);
            
            formData.append('Notes', $('#taskNotes').val().trim());
            formData.append('IsAll', $('#isAllCheckbox').is(':checked'));

            // Debug log
            console.log('Form validation passed. Preparing to send data:');
            console.log('Title:', $('#taskTitle').val().trim());
            console.log('IsAll:', $('#isAllCheckbox').is(':checked'));

            // Handle sharing options based on role
            if (role === 'Admin' && !$('#isAllCheckbox').is(':checked')) {
                // Get selected categories from checkboxes
                const selectedCategories = [];
                document.querySelectorAll('#categoryCheckboxList input[type="checkbox"]:checked').forEach(checkbox => {
                    selectedCategories.push(checkbox.value);
                    formData.append('SelectedBusinessCategories', checkbox.value);
                });
                console.log('Selected categories:', selectedCategories);
                
                // Get selected business owners from checkboxes
                const selectedOwners = [];
                const ownerCheckboxes = document.querySelectorAll('#businessOwnerCheckboxList input[type="checkbox"]:checked');
                console.log('Found checked business owner checkboxes:', ownerCheckboxes.length);
                
                ownerCheckboxes.forEach(checkbox => {
                    selectedOwners.push(checkbox.value);
                    formData.append('SelectedBusinessOwners', checkbox.value);
                    console.log(`Adding selected owner: ${checkbox.value} (${checkbox.getAttribute('data-name')})`);
                });
                console.log('Selected owners:', selectedOwners);
            } else if ((role === 'BusinessOwner' || (role === 'Staff' && canAdd)) && !$('#isAllCheckbox').is(':checked')) {
                // Get selected staff from checkboxes
                const selectedStaff = [];
                document.querySelectorAll('#staffCheckboxList input[type="checkbox"]:checked').forEach(checkbox => {
                    selectedStaff.push(checkbox.value);
                    formData.append('SelectedStaff', checkbox.value);
                });
                console.log('Selected staff:', selectedStaff);
            }
            
            // Log what we're sending
            console.log('Sending task data via JSON');
            
            // For admin users, show a loading message about sending emails
            if (role === 'Admin' && !$('#isAllCheckbox').is(':checked')) {
                const selectedOwnerCount = document.querySelectorAll('#businessOwnerCheckboxList input[type=\'checkbox\']:checked').length;
                const selectedCategoryCount = document.querySelectorAll('#categoryCheckboxList input[type=\'checkbox\']:checked').length;
                
                if (selectedOwnerCount > 0 || selectedCategoryCount > 0) {
                    // Show loading message
                    Swal.fire({
                        title: 'Processing',
                        html: `<p>Please wait while we process your appointment.</p>`,
                        icon: 'info',
                        allowOutsideClick: false,
                        allowEscapeKey: false,
                        showConfirmButton: false,
                        willOpen: () => {
                            Swal.showLoading();
                        }
                    });
                }
            }
            
            // Send task to server
            fetch('/Calendar/CreateTask', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                },
                body: JSON.stringify({
                    Title: $('#taskTitle').val().trim(),
                    Description: $('#taskNotes').val().trim(),
                    Date: $('#taskDate').val(),
                    EndDate: $('#taskEndDate').val() || $('#taskDate').val(),
                    Time: $('#taskTime').val() || '',
                    EndTime: $('#taskEndTime').val() || $('#taskTime').val() || '',
                    Priority: parseInt($('#taskPriority').val()), // Parse as integer to match enum
                    SendReminder: $('#isAllCheckbox').is(':checked'), // Using SendReminder field to store IsAll value
                    
                    // Add selected business categories and owners
                    SelectedBusinessCategories: Array.from(
                        document.querySelectorAll('#categoryCheckboxList input[type="checkbox"]:checked')
                    ).map(checkbox => checkbox.value),
                    
                    SelectedBusinessOwners: Array.from(
                        document.querySelectorAll('#businessOwnerCheckboxList input[type="checkbox"]:checked')
                    ).map(checkbox => checkbox.value),
                    
                    // Add selected staff members for business owners
                    SelectedStaff: Array.from(
                        document.querySelectorAll('#staffCheckboxList input[type="checkbox"]:checked')
                    ).map(checkbox => checkbox.value)
                })
            })
            .then(response => {
                if (!response.ok) {
                    // Try to get error details from response
                    return response.text().then(text => {
                        console.error('Server response:', text);
                        try {
                            // Try to parse as JSON
                            const jsonResponse = JSON.parse(text);
                            throw new Error(`Server returned ${response.status}: ${jsonResponse.message || text}`);
                        } catch (e) {
                            // If parsing fails, return the text
                        throw new Error(`Server returned ${response.status}: ${text}`);
                        }
                    });
                }
                return response.json();
            })
            .then(data => {
                if (data.success) {
                // Log the task data to debug
                console.log('Task data received from server:', data.task);
                
                // Make sure we have a valid task object with required fields
                if (!data.task) {
                    console.error('No task data returned from server');
                    Swal.fire({
                        icon: 'error',
                        title: 'Error',
                        text: 'No task data returned from server'
                    });
                    
                    // Restore button state
                    saveBtn.disabled = false;
                    saveBtn.innerHTML = originalText;
                    return;
                }
                
                // Close the modal
                taskModal.hide();

                // Restore button state
                saveBtn.disabled = false;
                saveBtn.innerHTML = originalText;

                // Show success message with sharing information for admins
                if (role === 'Admin') {
                    let sharingMessage = '';
                    
                    if ($('#isAllCheckbox').is(':checked')) {
                        sharingMessage = 'This appointment has been shared with all business owners.';
                    } else {
                        const selectedCategories = [];
                        document.querySelectorAll('#categoryCheckboxList input[type=\'checkbox\']:checked').forEach(checkbox => {
                            selectedCategories.push(checkbox.getAttribute('data-name'));
                        });
                        
                        const selectedOwners = [];
                        document.querySelectorAll('#businessOwnerCheckboxList input[type=\'checkbox\']:checked').forEach(checkbox => {
                            selectedOwners.push(checkbox.getAttribute('data-name'));
                        });
                        
                        if (selectedCategories.length > 0 || selectedOwners.length > 0) {
                            sharingMessage = 'This appointment has been shared with the selected business ';
                            
                            if (selectedCategories.length > 0) {
                                sharingMessage += 'categories' + (selectedOwners.length > 0 ? ' and ' : '.');
                            }
                            
                            if (selectedOwners.length > 0) {
                                sharingMessage += 'owners.';
                            }
                            
                            // Add notification message
                            sharingMessage += ' Email notifications have been sent.';
                        } else {
                            sharingMessage = 'This appointment is only visible to you.';
                        }
                    }
                    
                    Swal.fire({
                        icon: 'success',
                        title: 'Appointment Added Successfully',
                        html: `<p>${sharingMessage}</p>`,
                        showConfirmButton: true,
                        confirmButtonText: 'OK'
                    });
                } else {
                    // Standard success message for non-admin users
                    Swal.fire({
                        icon: 'success',
                        title: 'Success!',
                        text: 'Appointment added successfully',
                        timer: 2000,
                        showConfirmButton: false
                    });
                }
            } else {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: data.message || 'Failed to update task'
                });
            }
        })
        .catch(error => {
            console.error('Error updating task:', error);
            Swal.fire({
                icon: 'error',
                title: 'Error Adding Appointment',
                text: error.message || 'An unexpected error occurred'
            });
            
            // Restore button state
            saveBtn.disabled = false;
            saveBtn.innerHTML = originalText;
        });
    } catch (error) {
        console.error('Exception in addTask function:', error);
        Swal.fire({
            icon: 'error',
            title: 'Error',
            text: 'An unexpected error occurred: ' + error.message
        });
        
        // Restore button state
        saveBtn.disabled = false;
        saveBtn.innerHTML = originalText;
    }
}

    function deleteTask(taskId) {
        // Log the task ID being deleted
        console.log('Attempting to delete task with ID:', taskId);

        if (!taskId) {
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'Invalid task ID'
            });
            return;
        }

        // Verify task exists in our local array
        const taskExists = tasks.some(t => t.id === taskId);
        console.log('Task exists in local array:', taskExists);

        if (!taskExists) {
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'Task not found in local data'
            });
            return;
        }

        Swal.fire({
            title: 'Are you sure?',
            text: "This task will be permanently deleted!",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#dc3545',
            cancelButtonColor: '#6c757d',
            confirmButtonText: 'Yes, delete it!'
        }).then((result) => {
            if (result.isConfirmed) {
                // Get the current URL and construct the delete URL
                const baseUrl = window.location.origin;
                const deleteUrl = `${baseUrl}/Calendar/DeleteTask/${taskId}`;
                
                console.log('Sending delete request to:', deleteUrl);

                fetch(deleteUrl, {
                    method: 'DELETE',
                    headers: {
                        'Accept': 'application/json',
                        'Content-Type': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    credentials: 'same-origin'
                })
                .then(async response => {
                    console.log('Delete response status:', response.status);
                    
                    try {
                        const data = await response.json();
                        console.log('Delete response data:', data);
                        
                        if (!response.ok) {
                            throw new Error(data.message || 'Failed to delete task');
                        }
                        return data;
                    } catch (error) {
                        console.error('Error parsing response:', error);
                        if (response.status === 404) {
                            throw new Error(`Task not found with ID: ${taskId}`);
                        }
                        throw error;
                    }
                })
                .then(data => {
                    if (data.success) {
                        // Show success message - the actual removal will happen via SignalR
                        Swal.fire({
                            icon: 'success',
                            title: 'Deleted!',
                            text: data.message || 'Task has been deleted.',
                            timer: 2000,
                            showConfirmButton: false
                        });
                    } else {
                        throw new Error(data.message || 'Failed to delete task');
                    }
                })
                .catch(error => {
                    console.error('Error deleting task:', error);
                    Swal.fire({
                        icon: 'error',
                        title: 'Error',
                        text: error.message || 'Failed to delete task'
                    });
                });
            }
        });
    }

    function updateTaskStatus(taskId, isCompleted) {
        fetch(`/Calendar/UpdateTaskStatus?id=${taskId}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ isCompleted: isCompleted })
        })
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            if (data.success) {
                // Update local task
                const taskIndex = tasks.findIndex(t => t.id === taskId);
                if (taskIndex !== -1) {
                    tasks[taskIndex].isCompleted = isCompleted;
                }

                // Remove all instances of this task from the calendar
                cleanupTaskEvents(taskId);
                
                // Add updated event with completion status
                const task = tasks.find(t => t.id === taskId);
                if (task) {
                    calendar.addEvent({
                        id: task.id.toString(),
                        title: task.title,
                        start: task.time ? `${task.date}T${task.time}` : task.date,
                        allDay: !task.time,
                        extendedProps: {
                            taskId: task.id,
                            completed: isCompleted,
                            priority: task.priority,
                            priorityString: getPriorityString(task.priority),
                            notes: task.notes,
                            isPast: task.isPast,
                            // Include sharing properties
                            isAll: task.isAll,
                            boViewers: task.boViewers,
                            adminViewers1: task.adminViewers1,
                            adminViewers2: task.adminViewers2,
                            userId: task.userId,
                            userIdString: task.userIdString,
                            user: task.user,
                            whoSetAppointment: task.whoSetAppointment,
                            staffNameWhoCompleted: task.staffNameWhoCompleted
                        },
                        color: getPriorityColor(task.priority),
                        classNames: isCompleted ? ['completed-event'] : []
                    });
                }

                
            } else {
                showAlert('Failed to update task status', 'danger');
            }
        })
        .catch(error => {
            console.error('Error updating task status:', error);
            showAlert('Failed to update task status', 'danger');
        });
    }

    function editTask(task) {
        // Fill the form with task details
        document.getElementById('taskTitle').value = task.title;
        document.getElementById('taskDate').value = new Date(task.date).toISOString().split('T')[0];
        document.getElementById('taskTime').value = task.time ? formatTime(task.time) : '';
        document.getElementById('taskPriority').value = String(task.priority).toLowerCase();
        document.getElementById('taskNotes').value = task.notes || '';

        // Remove all instances of the task from the calendar
        cleanupTaskEvents(task.id);

        // Store the task ID for updating
        document.getElementById('addTask').dataset.taskId = task.id;
        document.getElementById('addTask').innerHTML = '<i class="fas fa-save me-2"></i>Update Task';
        document.getElementById('addTask').onclick = function() {
            updateTask(task.id);
        };

        // Focus on title field
        document.getElementById('taskTitle').focus();

        showAlert('Task loaded for editing', 'info');
    }

    function cleanupTaskEvents(taskId) {
        // Get all events
        const events = calendar.getEvents();
        
        // Remove any event with matching ID (both as string and number) or taskId in extendedProps
        events.forEach(event => {
            if (event.id === taskId.toString() || 
                event.id === taskId || 
                (event.extendedProps && event.extendedProps.taskId === taskId)) {
                event.remove();
            }
        });
    }

    function getPriorityColor(priority) {
        // Handle priority as either string or number (enum)
        if (typeof priority === 'number') {
            switch(priority) {
                case 2: return '#f72585'; // High
                case 1: return '#ff9f1c'; // Medium
                case 0: return '#2ec4b6'; // Low
                default: return '#4361ee';
            }
        } else {
            switch(priority) {
                case 'High': return '#f72585';
                case 'Medium': return '#ff9f1c';
                case 'Low': return '#2ec4b6';
                default: return '#4361ee';
            }
        }
    }

    // Convert priority enum to string
    function getPriorityString(priorityValue) {
        // Handle both string and number formats
        if (typeof priorityValue === 'number') {
            switch(priorityValue) {
                case 2: return 'High';
                case 1: return 'Medium';
                case 0: return 'Low';
                default: return 'Medium';
            }
        }
        return priorityValue || 'Medium';
    }

    function showAlert(message, type) {
        // Remove existing alerts
        var existingAlert = document.querySelector('.alert');
        if (existingAlert) {
            existingAlert.remove();
        }

        var alertHtml = `
            <div class="alert alert-${type} alert-dismissible fade show position-fixed top-0 end-0 m-3" role="alert" style="z-index: 1100;">
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', alertHtml);

        // Auto dismiss after 3 seconds
        setTimeout(() => {
            var alert = document.querySelector('.alert');
            if (alert) {
                var bsAlert = new bootstrap.Alert(alert);
                bsAlert.close();
            }
        }, 3000);
    }

    function resetTaskForm() {
        document.getElementById('taskId').value = '';
        document.getElementById('taskTitle').value = '';
        document.getElementById('taskDate').value = new Date().toISOString().split('T')[0];
        document.getElementById('taskEndDate').value = '';
        document.getElementById('taskTime').value = '';
        document.getElementById('taskEndTime').value = '';
        document.getElementById('taskPriority').value = '1'; // Medium (use string value)
        document.getElementById('taskNotes').value = '';

        // Additional clearing of checkboxes for different roles
        if (role === 'Admin') {
            // Clear all category checkboxes
            document.querySelectorAll('#categoryCheckboxList input[type="checkbox"]').forEach(checkbox => {
                checkbox.checked = false;
            });
            
            // Clear all business owner checkboxes
            document.querySelectorAll('#businessOwnerCheckboxList input[type="checkbox"]').forEach(checkbox => {
                checkbox.checked = false;
            });
        } else if (role === 'BusinessOwner' || role === 'Staff') {
            // Clear all staff checkboxes
            document.querySelectorAll('#staffCheckboxList input[type="checkbox"]').forEach(checkbox => {
                checkbox.checked = false;
            });
        }

        // Reset the "Make visible to all" checkbox
        const isAllCheckbox = document.getElementById('isAllCheckbox');
        if (isAllCheckbox) {
            isAllCheckbox.checked = false;
        }

        // Show sharing options
        if (role === 'Admin') {
            $('#adminSharingOptions').slideDown();
        } else if (role === 'BusinessOwner') {
            $('#boSharingOptions').slideDown();
        }
    }

    // Add this function to check if the current user can edit a task
    function canEditTask(task) {
        // Log task object for debugging
        console.log('canEditTask - checking task:', task);
        console.log('Current user ID:', userId, 'type:', typeof userId);
        console.log('Task user ID:', task.userId, 'type:', typeof task.userId);
        console.log('Task userIdString:', task.userIdString);
        
        // Use a safer comparison that handles undefined properly
        const taskUserId = task.userId !== undefined ? task.userId : 
                          (task.userIdString !== undefined ? task.userIdString : null);
        
        // Admins can edit any task
        if (role === 'Admin') {
            return true;
        }
        
        // Check if this is an admin-created appointment
        const isAdminCreated = task.whoSetAppointment == 1; // 1 = Admin in WhoSetAppointment enum
        
        // No one except admins can edit admin-created appointments
        if (isAdminCreated && role !== 'Admin') {
            return false;
        }
        
        // Business owners can only edit their own tasks
        if (role === 'BusinessOwner') {
            return taskUserId == userId;
        }

        // Staff with calendar access can edit appointments they created
        // or appointments shared with them by their business owner
        if (role === 'Staff' && staffCanEdit === true) {
            // Staff can edit their own appointments
            if (taskUserId == userId) {
                return true;
            }
            
            // Staff can also edit appointments shared with them specifically
            // or appointments marked as visible to all staff by their business owner
            // Check if the appointment is shared with all staff
            if (task.isAll === true) {
                return true;
            }
            
            // Check if the appointment is shared specifically with this staff member
            if (task.boViewers && task.boViewers.split(',').includes(userId)) {
                return true;
            }
            
            // For all other cases, staff cannot edit
            return false;
        }
        
        // Default: no edit permission
        return false;
    }

    // Add this near showTaskDetails function
    function showTaskDetails(event) {
        console.log('Showing details for task:', event);
        
        // Get task ID from event
        const taskId = event.extendedProps.taskId;
        console.log('Task ID:', taskId);
        
        // Find task in tasks array
        const task = tasks.find(t => t.id === taskId);
        if (!task) {
            console.error('Task not found in tasks array:', taskId);
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'Task details not found'
            });
            return;
        }
        
        console.log('Task found:', task);
        
        // Set task details in the modal
        document.getElementById('taskDetailsTitle').textContent = task.title;
        
        // Format the date for display
        const dateObj = new Date(task.date);
        const formattedDate = dateObj.toLocaleDateString('en-US', {
            weekday: 'long',
            year: 'numeric',
            month: 'long',
            day: 'numeric'
        });
        
        // Build the details content
        let detailsContent = `<p><strong>Date:</strong> ${formattedDate}</p>`;
        
        if (task.time) {
            detailsContent += `<p><strong>Time:</strong> ${formatTime(task.time)}</p>`;
        }
        
        if (task.endDate && task.endDate !== task.date) {
            const endDateObj = new Date(task.endDate);
            const formattedEndDate = endDateObj.toLocaleDateString('en-US', {
                weekday: 'long',
                year: 'numeric',
                month: 'long',
                day: 'numeric'
            });
            detailsContent += `<p><strong>End Date:</strong> ${formattedEndDate}</p>`;
        }
        
        if (task.endTime && task.endTime !== task.time) {
            detailsContent += `<p><strong>End Time:</strong> ${formatTime(task.endTime)}</p>`;
        }
        
        // Add priority
        const priorityString = getPriorityString(task.priority);
        let priorityBadgeClass = 'badge bg-success';
        if (priorityString === 'Medium') {
            priorityBadgeClass = 'badge bg-warning text-dark';
        } else if (priorityString === 'High') {
            priorityBadgeClass = 'badge bg-danger';
        }
        
        detailsContent += `<p><strong>Priority:</strong> <span class="${priorityBadgeClass}">${priorityString}</span></p>`;
        
        // Add notes if available
        if (task.notes) {
            detailsContent += `<p><strong>Notes:</strong></p><div class="task-notes p-2 border rounded bg-light">${task.notes}</div>`;
        }
        
        // Set the content
        document.getElementById('taskDetailsContent').innerHTML = detailsContent;
        
        // Clear the modal footer
        const modalFooter = document.getElementById('taskDetailsModalFooter');
        if (modalFooter) {
            modalFooter.innerHTML = '';
            
            // Check if the current user has permission to edit/delete this task
            const canEditDelete = canEditTask(task);
            console.log('Can edit/delete:', canEditDelete);
            
            // Add edit and delete buttons if user has permission
            if (canEditDelete) {
                // Create edit button
                const editButton = document.createElement('button');
                editButton.className = 'btn btn-primary me-2';
                editButton.innerHTML = '<i class="fas fa-edit"></i> Edit';
                editButton.onclick = function() {
                    // Close the details modal
                    $('#taskDetailsModal').modal('hide');
                    
                    // Open the edit modal with task data
                    openEditTaskModal(task);
                };
                modalFooter.appendChild(editButton);
                
                // Create delete button
                const deleteButton = document.createElement('button');
                deleteButton.className = 'btn btn-danger me-2';
                deleteButton.innerHTML = '<i class="fas fa-trash-alt"></i> Delete';
                deleteButton.onclick = function() {
                    // Confirm deletion
                    Swal.fire({
                        title: 'Delete Appointment?',
                        text: "This action cannot be undone.",
                        icon: 'warning',
                        showCancelButton: true,
                        confirmButtonColor: '#d33',
                        cancelButtonColor: '#3085d6',
                        confirmButtonText: 'Yes, delete it!'
                    }).then((result) => {
                        if (result.isConfirmed) {
                            // Close the details modal
                            $('#taskDetailsModal').modal('hide');
                            
                            // Delete the task
                            deleteTask(task.id);
                        }
                    });
                };
                modalFooter.appendChild(deleteButton);
            } else {
                // Check if the appointment was set by an admin and display a message
                const isAdminCreated = task.whoSetAppointment == 1; // 1 = Admin in WhoSetAppointment enum
                if (isAdminCreated) {
                    const adminMessage = document.createElement('div');
                    adminMessage.className = 'text-danger me-auto';
                    adminMessage.innerHTML = '<i class="fas fa-info-circle me-1"></i>This appointment is set by admin';
                    modalFooter.appendChild(adminMessage);
                }

                const isBoCreated = task.whoSetAppointment == 2; // 2 = Staff in WhoSetAppointment enum
                if (isBoCreated) {
                    const boMessage = document.createElement('div');
                    boMessage.className = 'text-danger me-auto';
                    boMessage.innerHTML = '<i class="fas fa-info-circle me-1"></i>You dont have permission to edit/delete this appointment';
                    modalFooter.appendChild(boMessage);
                }
            }
            
            // Always add a close button
            const closeButton = document.createElement('button');
            closeButton.className = 'btn btn-secondary';
            closeButton.setAttribute('data-bs-dismiss', 'modal');
            closeButton.innerHTML = 'Close';
            modalFooter.appendChild(closeButton);
        } else {
            console.error('Modal footer element not found with ID: taskDetailsModalFooter');
        }
        
        // Show the modal
        $('#taskDetailsModal').modal('show');
    }

    // Function to open the edit task modal with task data
    function openEditTaskModal(task) {
        console.log('Opening edit modal for task:', task);
        
        // Reset form first - this is already called but we need to make sure it fully clears everything
        resetTaskForm();
        
        // Set modal title
        document.getElementById('taskModalTitle').textContent = 'Edit Appointment';
        
        // Set mode for save button
        document.getElementById('saveTask').setAttribute('data-mode', 'edit');
        document.getElementById('saveTask').textContent = 'Update Appointment';
        
        // Set task ID in hidden field
        document.getElementById('taskId').value = task.id;
        
        // Populate form fields with task data
        document.getElementById('taskTitle').value = task.title;
        document.getElementById('taskDate').value = task.date;
        document.getElementById('taskEndDate').value = task.endDate || task.date;
        document.getElementById('taskTime').value = task.time || '';
        document.getElementById('taskEndTime').value = task.endTime || task.time || '';
        
        // Set priority correctly based on type
        if (typeof task.priority === 'number') {
            document.getElementById('taskPriority').value = task.priority;
        } else if (typeof task.priority === 'string') {
            switch (task.priority) {
                case "Low":
                    document.getElementById('taskPriority').value = 0;
                    break;
                case "Medium":
                    document.getElementById('taskPriority').value = 1;
                    break;
                case "High":
                    document.getElementById('taskPriority').value = 2;
                    break;
                default:
                    document.getElementById('taskPriority').value = 1; // Default to medium
            }
        } else {
            document.getElementById('taskPriority').value = 1; // Default to medium
        }
        
        document.getElementById('taskNotes').value = task.notes || '';
        
        // Set isAll checkbox and handle UI visibility
        document.getElementById('isAllCheckbox').checked = task.isAll;
        if (task.isAll) {
            $('#adminSharingOptions, #boSharingOptions').hide();
        } else {
            if (role === 'Admin') {
                $('#adminSharingOptions').show();
            } else if (role === 'BusinessOwner') {
                $('#boSharingOptions').show();
            }
        }

        const checkIcon = document.querySelectorAll('#check-icon');
        if (checkIcon.length > 0) {
            checkIcon.forEach(icon => {
                icon.style.display = 'none';
            });
        }

        const categoryBadge = document.querySelectorAll('#category-badge');
        if (categoryBadge.length > 0) {
            categoryBadge.forEach(badge => {
                badge.style.display = 'none';
            });
        }
        
        // Handle sharing options based on role with proper delay to ensure DOM is ready
        if (role === 'Admin' && !task.isAll) {
            // Set selected categories if any
            if (task.adminViewers1) {
                const categoryIds = task.adminViewers1.split(',');
                console.log('Setting categories from:', categoryIds);
                
                categoryIds.forEach(category => {
                    const checkbox = document.querySelector(`#category-${category.replace(/\s+/g, '-')}`);
                    if (checkbox) {
                        checkbox.checked = true;
                    }
                });
                // Update business owners list based on selected categories
                updateBusinessOwnersList();
            }
            
            // Set selected business owners if any
            if (task.adminViewers2) {
                console.log('Setting business owners from:', task.adminViewers2);
                // Ensure we're working with a string and then split it
                const ownersString = typeof task.adminViewers2 === 'string' ? task.adminViewers2 : task.adminViewers2.toString();
                const owners = ownersString.split(',').filter(id => id.trim() !== '');
                console.log('Owner IDs to select:', owners);
                
                // Log available checkboxes
                const availableCheckboxes = document.querySelectorAll('#businessOwnerCheckboxList input[type="checkbox"]');
                console.log('Available owner checkboxes:', availableCheckboxes.length);
                
                // Try multiple strategies to select checkboxes
                owners.forEach(ownerId => {
                    console.log('Looking for owner checkbox with ID:', `owner-${ownerId}`);
                    
                    // First, try ID-based selection
                    const checkboxById = document.getElementById(`owner-${ownerId}`);
                    if (checkboxById) {
                        console.log('Found checkbox by ID, setting checked');
                        checkboxById.checked = true;
                        return; // Continue to next owner
                    }
                    
                    // Try finding by CSS selector
                    const checkboxBySelector = document.querySelector(`#owner-${ownerId}`);
                    if (checkboxBySelector) {
                        console.log('Found checkbox by selector, setting checked');
                        checkboxBySelector.checked = true;
                        return; // Continue to next owner
                    }
                    
                    // Try finding by value attribute
                    const valueCheckbox = document.querySelector(`#businessOwnerCheckboxList input[value="${ownerId}"]`);
                    if (valueCheckbox) {
                        console.log('Found checkbox by value attribute, setting checked');
                        valueCheckbox.checked = true;
                        return; // Continue to next owner
                    }
                    
                    // Try finding any checkbox that might match
                    availableCheckboxes.forEach(checkbox => {
                        // Check if the value matches with or without string conversion
                        if (checkbox.value === ownerId || checkbox.value === ownerId.toString()) {
                            console.log('Found checkbox by value equality, setting checked');
                            checkbox.checked = true;
                        }
                    });
                    
                    console.log('Could not find checkbox for owner ID:', ownerId);
                });
                
                // Count how many were checked
                const checkedBoxes = document.querySelectorAll('#businessOwnerCheckboxList input[type="checkbox"]:checked');
                console.log('Successfully checked', checkedBoxes.length, 'business owner checkboxes');
            }
        } else if (role === 'BusinessOwner' && !task.isAll) {
            // Set selected staff if any
            if (task.boViewers) {
                const staffIds = task.boViewers.split(',');
                staffIds.forEach(staffId => {
                    const checkbox = document.querySelector(`#staff-${staffId}`);
                    if (checkbox) {
                        checkbox.checked = true;
                    }
                });
            }
        }
        
        // Show the modal
        const taskModal = new bootstrap.Modal(document.getElementById('taskModal'));
        taskModal.show();
    }

    function validateTaskForm() {
        // Check required fields
        const title = document.getElementById('taskTitle').value.trim();
        const date = document.getElementById('taskDate').value;
        
        if (!title) {
            Swal.fire({
                icon: 'warning',
                title: 'Missing Information',
                text: 'Please enter a task title'
            });
            return false;
        }
        
        if (!date) {
            Swal.fire({
                icon: 'warning',
                title: 'Missing Information',
                text: 'Please select a date'
            });
            return false;
        }
        
        return true;
    }

    function updateTask(taskId, saveBtn, originalText) {
        try {
            // Validate required fields first
            if (!validateTaskForm()) {
                // Restore button on validation failure
                if (saveBtn) {
                    saveBtn.disabled = false;
                    saveBtn.innerHTML = originalText;
                }
                return;
            }
            
            // Create a FormData object
            const formData = new FormData();
            formData.append('__RequestVerificationToken', $('input[name="__RequestVerificationToken"]').val());
            
            // Add the task ID
            formData.append('Id', taskId);
            
            // Map form fields directly to CalendarTaskDTO properties
            formData.append('Title', $('#taskTitle').val().trim());
            formData.append('Date', $('#taskDate').val());
            formData.append('EndDate', $('#taskEndDate').val() || $('#taskDate').val()); // Use start date if end date is not provided
            formData.append('Time', $('#taskTime').val() || null);
            formData.append('EndTime', $('#taskEndTime').val() || $('#taskTime').val()); // Use start time if end time is not provided
            
            // Ensure priority is a number
            const priorityValue = parseInt($('#taskPriority').val());
            formData.append('Priority', priorityValue);
            console.log('Sending priority value:', priorityValue);
            
            formData.append('Notes', $('#taskNotes').val().trim());
            formData.append('IsAll', $('#isAllCheckbox').is(':checked'));
            
            // Debug log
            console.log('Updating appointment ID:', taskId);
            console.log('Title:', $('#taskTitle').val().trim());
            console.log('IsAll:', $('#isAllCheckbox').is(':checked'));

            // Handle sharing options based on role
            if (role === 'Admin' && !$('#isAllCheckbox').is(':checked')) {
                // Get selected categories from checkboxes
                const selectedCategories = [];
                document.querySelectorAll('#categoryCheckboxList input[type="checkbox"]:checked').forEach(checkbox => {
                    selectedCategories.push(checkbox.value);
                    formData.append('SelectedBusinessCategories', checkbox.value);
                });
                console.log('Selected categories:', selectedCategories);
                
                // Get selected business owners from checkboxes
                const selectedOwners = [];
                const ownerCheckboxes = document.querySelectorAll('#businessOwnerCheckboxList input[type="checkbox"]:checked');
                console.log('Found checked business owner checkboxes:', ownerCheckboxes.length);
                
                ownerCheckboxes.forEach(checkbox => {
                    selectedOwners.push(checkbox.value);
                    formData.append('SelectedBusinessOwners', checkbox.value);
                    console.log(`Adding selected owner: ${checkbox.value} (${checkbox.getAttribute('data-name')})`);
                });
                console.log('Selected owners:', selectedOwners);
            } else if (role === 'BusinessOwner' && !$('#isAllCheckbox').is(':checked')) {
                // Get selected staff from checkboxes
                const selectedStaff = [];
                document.querySelectorAll('#staffCheckboxList input[type="checkbox"]:checked').forEach(checkbox => {
                    selectedStaff.push(checkbox.value);
                    formData.append('SelectedStaff', checkbox.value);
                });
                console.log('Selected staff:', selectedStaff);
            }
            
            console.log('Sending update for task ID:', taskId);
            
            // Send updated task to server
            fetch('/Calendar/UpdateTask', {
                method: 'PUT',
                body: formData
            })
            .then(response => {
                if (!response.ok) {
                    // Try to get error details from response
                    return response.text().then(text => {
                        console.error('Server response:', text);
                        try {
                            // Try to parse as JSON
                            const jsonResponse = JSON.parse(text);
                            throw new Error(`Server returned ${response.status}: ${jsonResponse.message || text}`);
                        } catch (e) {
                            // If parsing fails, return the text
                            throw new Error(`Server returned ${response.status}: ${text}`);
                        }
                    });
                }
                return response.json();
            })
            .then(data => {
                if (data.success) {
                    // Close the modal
                    taskModal.hide();
                    
                    // Restore button state if provided
                    if (saveBtn) {
                        saveBtn.disabled = false;
                        saveBtn.innerHTML = originalText;
                    }
                    
                    // Show success message
                    Swal.fire({
                        icon: 'success',
                        title: 'Success!',
                        text: 'Appointment updated successfully',
                        timer: 2000,
                        showConfirmButton: false
                    });
                } else {
                    Swal.fire({
                        icon: 'error',
                        title: 'Error',
                        text: data.message || 'Failed to update appointment'
                    });
                }
            })
            .catch(error => {
                console.error('Error updating appointment:', error);
                Swal.fire({
                    icon: 'error',
                    title: 'Error Updating Appointment',
                    text: error.message || 'An unexpected error occurred'
                });
                
                // Restore button state if provided
                if (saveBtn) {
                    saveBtn.disabled = false;
                    saveBtn.innerHTML = originalText;
                }
            });
        } catch (error) {
            console.error('Exception in updateTask function:', error);
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'An unexpected error occurred: ' + error.message
            });
            
            // Restore button state if provided
            if (saveBtn) {
                saveBtn.disabled = false;
                saveBtn.innerHTML = originalText;
            }
        }
    }
    
    // Add handler for isAllCheckbox to toggle sharing options visibility
    $(document).on('change', '#isAllCheckbox', function() {
        console.log('isAllCheckbox changed:', this.checked);
        if (this.checked) {
            // Hide sharing options when "Make visible to all" is checked
            console.log('Hiding sharing options');
            $('#adminSharingOptions, #boSharingOptions').hide();
        } else {
            // Show sharing options based on role
            console.log('Showing sharing options for role:', role);
            if (role === 'Admin') {
                $('#adminSharingOptions').show();
            } else if (role === 'BusinessOwner') {
                $('#boSharingOptions').show();
            }
        }
    });
});

// Add click handler for the Check Today's Events button
$('#checkTodayEvents').on('click', function() {
    $(this).prop('disabled', true);
    $(this).html('<i class="fas fa-spinner fa-spin me-2"></i> Checking...');
    
    $.ajax({
        url: '/Calendar/GetTodayEvents',
        type: 'GET',
        success: function(response) {
            if (response.success) {
                if (response.events.length > 0) {
                    Swal.fire({
                        title: 'Today\'s Events',
                        html: `<p>You have ${response.events.length} event(s) scheduled for today.</p>
                              <p>A notification has been sent to your email with details.</p>`,
                        icon: 'info',
                        confirmButtonText: 'OK'
                    });
                } else {
                    Swal.fire({
                        title: 'No Events Today',
                        text: 'You have no events scheduled for today.',
                        icon: 'info',
                        confirmButtonText: 'OK'
                    });
                }
            } else {
                Swal.fire({
                    title: 'Error',
                    text: 'Failed to retrieve today\'s events.',
                    icon: 'error',
                    confirmButtonText: 'OK'
                });
            }
            
            $('#checkTodayEvents').prop('disabled', false);
            $('#checkTodayEvents').html('<i class="fas fa-calendar-day me-2"></i> Check Today\'s Events');
        },
        error: function(error) {
            console.error('Error checking today\'s events:', error);
            
            Swal.fire({
                title: 'Error',
                text: 'Failed to retrieve today\'s events.',
                icon: 'error',
                confirmButtonText: 'OK'
            });
            
            $('#checkTodayEvents').prop('disabled', false);
            $('#checkTodayEvents').html('<i class="fas fa-calendar-day me-2"></i> Check Today\'s Events');
        }
    });
});