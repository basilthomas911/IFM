#![cfg(windows)]

use core::ffi::c_void;
use core::ptr;

use crate::abi::{
    NO_MEMORY, NUMA_CONFIGURATION_FAILED, OK, OS_ERROR, Status, TIMEOUT, WAIT_INFINITE,
};

type Handle = *mut c_void;

const MEM_COMMIT: u32 = 0x1000;
const MEM_RESERVE: u32 = 0x2000;
const MEM_RELEASE: u32 = 0x8000;
const PAGE_READWRITE: u32 = 0x04;
const WAIT_OBJECT_0: u32 = 0;
const WAIT_TIMEOUT: u32 = 258;
const INFINITE: u32 = 0xffff_ffff;
const THREAD_PRIORITY_NORMAL: i32 = 0;
const THREAD_PRIORITY_ABOVE_NORMAL: i32 = 1;
const THREAD_PRIORITY_HIGHEST: i32 = 2;

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct ProcessorNumber {
    pub group: u16,
    pub number: u8,
    pub reserved: u8,
}

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct GroupAffinity {
    pub mask: usize,
    pub group: u16,
    pub reserved: [u16; 3],
}

#[link(name = "kernel32")]
unsafe extern "system" {
    fn VirtualAlloc(
        address: *mut c_void,
        size: usize,
        allocation_type: u32,
        protect: u32,
    ) -> *mut c_void;
    fn VirtualAllocExNuma(
        process: Handle,
        address: *mut c_void,
        size: usize,
        allocation_type: u32,
        protect: u32,
        preferred_node: u32,
    ) -> *mut c_void;
    fn VirtualFree(address: *mut c_void, size: usize, free_type: u32) -> i32;
    fn VirtualLock(address: *mut c_void, size: usize) -> i32;
    fn VirtualUnlock(address: *mut c_void, size: usize) -> i32;
    fn GetCurrentProcess() -> Handle;
    fn CreateEventW(
        attributes: *const c_void,
        manual_reset: i32,
        initial_state: i32,
        name: *const u16,
    ) -> Handle;
    fn SetEvent(event: Handle) -> i32;
    fn WaitForSingleObject(handle: Handle, milliseconds: u32) -> u32;
    fn CloseHandle(handle: Handle) -> i32;
    fn GetCurrentThread() -> Handle;
    fn SetThreadGroupAffinity(
        thread: Handle,
        affinity: *const GroupAffinity,
        previous: *mut GroupAffinity,
    ) -> i32;
    fn GetThreadGroupAffinity(thread: Handle, affinity: *mut GroupAffinity) -> i32;
    fn GetCurrentProcessorNumberEx(processor_number: *mut ProcessorNumber);
    fn SetThreadPriority(thread: Handle, priority: i32) -> i32;
    fn QueryPerformanceCounter(counter: *mut i64) -> i32;
    fn QueryPerformanceFrequency(frequency: *mut i64) -> i32;
}

/// Converts the Windows high-resolution counter directly to nanoseconds.
pub struct PerformanceClock {
    frequency: i64,
    integral_nanoseconds_per_tick: i64,
}

impl PerformanceClock {
    pub fn new() -> Result<Self, Status> {
        let mut frequency = 0i64;
        if unsafe { QueryPerformanceFrequency(&mut frequency) } == 0 || frequency <= 0 {
            return Err(OS_ERROR);
        }
        Ok(Self {
            frequency,
            integral_nanoseconds_per_tick: if 1_000_000_000 % frequency == 0 {
                1_000_000_000 / frequency
            } else {
                0
            },
        })
    }

    #[inline(always)]
    pub fn now_nanoseconds(&self) -> i64 {
        let mut counter = 0i64;
        if unsafe { QueryPerformanceCounter(&mut counter) } == 0 {
            return 0;
        }
        if self.integral_nanoseconds_per_tick != 0 {
            counter * self.integral_nanoseconds_per_tick
        } else {
            ((i128::from(counter) * 1_000_000_000i128) / i128::from(self.frequency)) as i64
        }
    }
}

pub struct Pages {
    ptr: *mut u8,
    bytes: usize,
    locked: bool,
}
unsafe impl Send for Pages {}
unsafe impl Sync for Pages {}

impl Pages {
    pub fn allocate(bytes: usize, numa_node: u16, require_numa: bool) -> Result<Self, Status> {
        let mut memory = unsafe {
            if numa_node == u16::MAX {
                VirtualAlloc(
                    ptr::null_mut(),
                    bytes,
                    MEM_RESERVE | MEM_COMMIT,
                    PAGE_READWRITE,
                )
            } else {
                VirtualAllocExNuma(
                    GetCurrentProcess(),
                    ptr::null_mut(),
                    bytes,
                    MEM_RESERVE | MEM_COMMIT,
                    PAGE_READWRITE,
                    numa_node.into(),
                )
            }
        };
        if memory.is_null() && numa_node != u16::MAX && !require_numa {
            memory = unsafe {
                VirtualAlloc(
                    ptr::null_mut(),
                    bytes,
                    MEM_RESERVE | MEM_COMMIT,
                    PAGE_READWRITE,
                )
            };
        }
        if memory.is_null() {
            return Err(if numa_node != u16::MAX && require_numa {
                NUMA_CONFIGURATION_FAILED
            } else {
                NO_MEMORY
            });
        }
        unsafe { ptr::write_bytes(memory, 0, bytes) };
        Ok(Self {
            ptr: memory.cast(),
            bytes,
            locked: false,
        })
    }

    pub fn as_ptr<T>(&self) -> *mut T {
        self.ptr.cast()
    }
    pub fn lock(&mut self) -> bool {
        self.locked = unsafe { VirtualLock(self.ptr.cast(), self.bytes) != 0 };
        self.locked
    }
}

impl Drop for Pages {
    fn drop(&mut self) {
        unsafe {
            if self.locked {
                let _ = VirtualUnlock(self.ptr.cast(), self.bytes);
            }
            let _ = VirtualFree(self.ptr.cast(), 0, MEM_RELEASE);
        }
    }
}

pub struct Signal(Handle);
unsafe impl Send for Signal {}
unsafe impl Sync for Signal {}

impl Signal {
    pub fn new() -> Result<Self, Status> {
        let handle = unsafe { CreateEventW(ptr::null(), 0, 0, ptr::null()) };
        if handle.is_null() {
            Err(OS_ERROR)
        } else {
            Ok(Self(handle))
        }
    }
    pub fn notify(&self) {
        unsafe {
            let _ = SetEvent(self.0);
        }
    }
    pub fn wait(&self, timeout_ms: u32) -> Status {
        let result = unsafe {
            WaitForSingleObject(
                self.0,
                if timeout_ms == WAIT_INFINITE {
                    INFINITE
                } else {
                    timeout_ms
                },
            )
        };
        if result == WAIT_OBJECT_0 {
            OK
        } else if result == WAIT_TIMEOUT {
            TIMEOUT
        } else {
            OS_ERROR
        }
    }
}

impl Drop for Signal {
    fn drop(&mut self) {
        unsafe {
            let _ = CloseHandle(self.0);
        }
    }
}

pub fn current_processor_location() -> u32 {
    let mut processor = ProcessorNumber::default();
    unsafe { GetCurrentProcessorNumberEx(&mut processor) };
    (u32::from(processor.group) << 16) | u32::from(processor.number)
}

pub fn set_thread_affinity(group: u16, logical_processor: u16) -> bool {
    if logical_processor >= usize::BITS as u16 {
        return false;
    }
    let requested = GroupAffinity {
        mask: 1usize << logical_processor,
        group,
        reserved: [0; 3],
    };
    if unsafe { SetThreadGroupAffinity(GetCurrentThread(), &requested, ptr::null_mut()) } == 0 {
        return false;
    }
    let mut observed = GroupAffinity::default();
    unsafe {
        GetThreadGroupAffinity(GetCurrentThread(), &mut observed) != 0
            && observed.group == group
            && observed.mask == requested.mask
    }
}

pub fn set_thread_priority(priority: i32) -> bool {
    let native = match priority {
        1 => THREAD_PRIORITY_ABOVE_NORMAL,
        2.. => THREAD_PRIORITY_HIGHEST,
        _ => THREAD_PRIORITY_NORMAL,
    };
    unsafe { SetThreadPriority(GetCurrentThread(), native) != 0 }
}
