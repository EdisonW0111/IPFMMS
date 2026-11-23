(() => {
    const form=document.getElementById('insightsFilter'),results=document.getElementById('insightsResults');
    if(!form||!results)return;
    const month=document.getElementById('insightsMonth'),loading=document.getElementById('insightsLoading'),error=document.getElementById('insightsError');
    let pending=null,version=0;
    async function load(){
        if(pending)pending.abort();const current=++version;
        loading.classList.add('d-none');error.classList.add('d-none');results.setAttribute('aria-busy','false');
        if(!form.reportValidity())return;
        const controller=new AbortController();pending=controller;
        const url=new URL(form.action,window.location.href);url.searchParams.set('month',month.value);
        loading.classList.remove('d-none');results.setAttribute('aria-busy','true');
        try{
            const response=await fetch(url,{headers:{'X-Requested-With':'XMLHttpRequest'},credentials:'same-origin',cache:'no-store',signal:controller.signal});
            if(!response.ok||response.redirected)throw new Error('Unable to load insights');
            const html=await response.text();if(current!==version)return;
            results.innerHTML=html;
            const actual=results.querySelector('.insights-overview')?.dataset.month;
            if(actual){month.value=actual;url.searchParams.set('month',actual);}
            window.history.replaceState(null,'',url.toString());
        }catch(failure){if(failure.name!=='AbortError'&&current===version)error.classList.remove('d-none');}
        finally{if(current===version){loading.classList.add('d-none');results.setAttribute('aria-busy','false');pending=null;}}
    }
    month.addEventListener('change',load);
    form.querySelector('button[type="submit"]')?.setAttribute('hidden', '');
    form.addEventListener('submit',e=>{e.preventDefault();load();});
    document.getElementById('insightsRetry').addEventListener('click',load);
})();
